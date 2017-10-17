// <copyright file="Program.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Zhaobang.FtpServer
{
    /// <summary>
    /// The main program
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The entry point of the program
        /// </summary>
        /// <param name="args">Argumentss entered in the shell/param>
        private static void Main(string[] args)
        {
            Console.WriteLine("FTP Server by Zhaobang China");
            Console.WriteLine("Server version: {0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Library version: {0}", System.Reflection.Assembly.GetAssembly(typeof(FtpServer)).GetName().Version);
            Console.WriteLine("This is an open source project. Project site: https://github.com/ZhaobangChina/FtpServer/tree/master/Server");
            Console.WriteLine();

            RunConfig config = RunConfig.Default;
            XmlSerializer serializer = new XmlSerializer(typeof(RunConfig));
            FileInfo configFileInfo = new FileInfo(Path.Combine(Environment.CurrentDirectory, "RunConfig.xml"));

            if (configFileInfo.Exists)
            {
                using (Stream configFileStream = configFileInfo.OpenRead())
                {
                    config = serializer.Deserialize(configFileStream) as RunConfig;
                }
            }
            else
            {
                using (Stream configFileStream = configFileInfo.OpenWrite())
                {
                    Console.WriteLine("Creating default config file at {0}", configFileInfo.FullName);
                    serializer.Serialize(configFileStream, config);
                    Console.WriteLine("Default config file created");
                }
            }

            var cancelSource = new CancellationTokenSource();
            var runResults = config.EndPoints.Select(
                ep => new FtpServer(ep, config.BaseDirectory)
                    .RunAsync(cancelSource.Token)
                    .ContinueWith(result =>
                    {
                        if (result.Exception != null)
                        {
                            Console.WriteLine($"Server at {ep} has stopped because of: \n{result.Exception.Message}\n");
                        }
                        else
                        {
                            Console.WriteLine($"Server at {ep} has stopped successfully.");
                        }
                    }))
                .ToArray();

            Console.WriteLine("FTP server has been started.");
            Console.WriteLine("Config file: \n\t{0}", configFileInfo.FullName);
            Console.WriteLine("Root dir: \n\t{0}", config.BaseDirectory);
            Console.WriteLine(
                "End points:\n\t{0}",
                string.Join("\n\t", config.EndPoints.Select(ep => ep.ToString())));
            Console.WriteLine("Use command \"quit\" to stop FTP server");

            while (true)
            {
                var command = Console.ReadLine();
                if (command.ToUpper() == "QUIT")
                {
                    cancelSource.Cancel();
                    Console.WriteLine("Stopped accepting new connections. Waiting until all clients quit.");
                    Task.WaitAll(runResults);
                    Console.WriteLine("Quited.");
                    return;
                }
            }
        }
    }
}
