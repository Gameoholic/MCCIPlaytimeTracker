using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MinecraftLogProgram
{
    internal class Program
    {

        //todo: if a player joins but is immediately kicked (whitelist, auth servers down) the player can farm time played on a survival world.
        //todo: won't work on other servers that don't have a resource pack
        //todo: doesn't work on fabric if played for more than 24h
        //todo: vanilla support
        static void Main(string[] args)
        {
            int seconds = 0;
            int timesJoined = 0;
            int timesLeft = 0;


            Console.WriteLine("DISCLAIMER: Only tested on Forge/Fabric.");
            Console.WriteLine("I'd recommend to copy all log files from 2022 to 2023 into a new folder just so it wouldn't have to scan files from before MCCI released " 
                +"(I'm too lazy to code it).");
            Console.Write("Enter the logs folder path: ");
            string folderPath = Console.ReadLine();
            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"folder {folderPath} does not exist noob try again");
                return;
            }
            string[] filePaths = Directory.GetFiles(folderPath, "*.log.gz");
            Console.WriteLine($"Scanning {filePaths.Length} log files. (insert quirky loading message...)");


            bool isFabric = false;

            string joinMessageHeader = "[Render thread/INFO] [net.minecraft.client.gui.screens.ConnectScreen/]: ";
            string leaveMessageHeader = "[Render thread/INFO] [net.minecraft.client.renderer.texture.TextureAtlas/]: ";

            int errors = 0;


            foreach (string filePath in filePaths)
            {
                bool connectedToServer = false;
                DateTime serverJoinTime = DateTime.MinValue;
                string lastDateString = "";
                
                try
                {
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                    using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                    using (StreamReader reader = new StreamReader(gzipStream))
                    {
                        string line;
                        Console.WriteLine($"Scanning {filePath}");
                        while ((line = reader.ReadLine()) != null)
                        {
                            // Extract date and text
                            Match match = Regex.Match(line, @"\[(.+?)\] (.+)");
                            if (!match.Success)
                            {
                                continue;
                            }
                            string dateString = match.Groups[1].Value;
                            string text = match.Groups[2].Value;
                            lastDateString = dateString;

                            if (text.StartsWith("[main/INFO]: Loading Minecraft") && text.Contains("Fabric"))
                            {
                                isFabric = true;
                                joinMessageHeader = "[Render thread/INFO]: ";
                                leaveMessageHeader = "[Render thread/INFO]: ";
                            }



                            // Check if connected to server
                            if (text.Equals(joinMessageHeader + "Connecting to play.mccisland.net, 25565") ||
                                text.Equals(joinMessageHeader + "Connecting to alt.mccisland.net, 25565"))
                            {
                                connectedToServer = true;
                                if (!isFabric)
                                    serverJoinTime = DateTime.ParseExact(dateString, "ddMMMyyyy HH:mm:ss.fff", null);
                                else
                                    serverJoinTime = DateTime.ParseExact(dateString, "HH:mm:ss", null);
                                Console.WriteLine("joined server at " + serverJoinTime);
                                timesJoined++;
                            }

                            // Check if left server and calculate time difference
                            if (text.Equals(leaveMessageHeader + "Created: 1024x512x4 minecraft:textures/atlas/blocks.png-atlas") &&
                                connectedToServer)
                            {
                                DateTime serverLeaveTime;
                                if (!isFabric)
                                    serverLeaveTime = DateTime.ParseExact(dateString, "ddMMMyyyy HH:mm:ss.fff", null);
                                else
                                {
                                    serverLeaveTime = DateTime.ParseExact(dateString, "HH:mm:ss", null);

                                    // Extract the date from the file name
                                    string fileName = Path.GetFileName(filePath);
                                    if (!Regex.IsMatch(fileName, @"^\d{4}-\d{2}-\d{2}-\d+\.log\.gz$"))
                                    {
                                        continue;
                                    }
                                    string dateString2 = fileName.Substring(0, 10);

                                    var dateStringElements = lastDateString.Split('-');
                                    DateTime fileDate = DateTime.ParseExact($"{dateStringElements[0]}-{dateStringElements[1]}-{dateStringElements[2]}",
                                        "yyyy-MM-dd", CultureInfo.InvariantCulture);

                                    DateTime newDateTime = new DateTime(fileDate.Year, fileDate.Month, fileDate.Day,
                                                                        serverLeaveTime.Hour, serverLeaveTime.Minute, serverLeaveTime.Second);
                                    serverLeaveTime = newDateTime;


                                }
                                Console.WriteLine("left server at " + serverLeaveTime);

                                TimeSpan timeDiff = serverLeaveTime - serverJoinTime;
                                seconds += Math.Abs((int)timeDiff.TotalSeconds);

                                connectedToServer = false;
                                timesLeft++;
                            }
                            //fix being able to farm time on another server by getting kicked before loading into the world
                            if (text.StartsWith(joinMessageHeader + "Connecting to ") &&
                                !text.EndsWith("play.mccisland.net, 25565") && !text.EndsWith("alt.mccisland.net, 25565"))
                            {
                                connectedToServer = false;
                            }
                        }
                    }

                    if (connectedToServer)
                    {
                        DateTime serverLeaveTime;

                        if (!isFabric)
                            serverLeaveTime = DateTime.ParseExact(lastDateString, "ddMMMyyyy HH:mm:ss.fff", null);
                        else
                        {
                            serverLeaveTime = DateTime.ParseExact(lastDateString, "HH:mm:ss", null);

                            // Extract the date from the file name
                            string fileName = Path.GetFileName(filePath);


                            if (!fileName.EndsWith(".log.gz"))
                            {
                                continue;
                            }
                            string dateString2 = fileName.Substring(0, 10);



                            var dateStringElements = dateString2.Split('-');

                            DateTime fileDate = DateTime.ParseExact($"{dateStringElements[0]}-{dateStringElements[1]}-{dateStringElements[2]}",
                                "yyyy-MM-dd", CultureInfo.InvariantCulture);

                            DateTime newDateTime = new DateTime(fileDate.Year, fileDate.Month, fileDate.Day,
                                                                serverLeaveTime.Hour, serverLeaveTime.Minute, serverLeaveTime.Second);
                            //serverLeaveTime = newDateTime;


                        }
                        Console.WriteLine("left server and quit client at " + serverLeaveTime);

                        TimeSpan timeDiff = serverLeaveTime - serverJoinTime;
                        seconds += Math.Abs((int)timeDiff.TotalSeconds);
                        Console.WriteLine("Session playtime: " + Math.Abs((int)timeDiff.TotalSeconds) / 3600.0 + " hours");

                        timesLeft++;
                    }
                    
                }
                
           
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR scanning file {filePath}: {e.Message}");
                    errors += 1;
                }
            }

            int hours = seconds / 3600;
            Console.WriteLine($"Finished scan with {errors} errors.");
            Console.WriteLine("\n\n\n\n\n");
            Console.WriteLine("Total hours spent on the server: " + hours);
            Console.WriteLine($"Times joined: {timesJoined}");
            Console.WriteLine($"Times left: {timesLeft}");

            Console.WriteLine("Now for some fun stats that will possibly make you regret spending this amount of time on mcci:");

            var activities = new Dictionary<string, double>(){
            {"You could've cooked pasta X times!", 0.15},
            {"You could've watched Game of Thrones X times!", 70.2},
            {"You could've watched the entirety of One Piece X times!", 381.3},
            {"You could've driven across the US X times!", 45},
            {"You could've watched the Bee Movie X times!", 1.52},
            {"You could've watched the entire LOTR trilogy X times!", 2.97},
            {"You could've watched the MCC admin streams X times!", 85.5},
            {"You could've watched the entirety of Critical Role Campaign 1 X times!", 447},
            };

            Console.WriteLine($"You could've made ${7.25 * hours} working a minimum wage job in the US!");
            

            foreach (KeyValuePair<string, double> activity in activities)
            {
                double activityValue = hours / activity.Value;
                string formattedValue = activityValue.ToString("F2");
                Console.WriteLine(activity.Key.Replace("X", formattedValue));
            }

            Console.WriteLine("\nPress any program to leave the key.");
            Console.ReadLine();
        }
    }
}
