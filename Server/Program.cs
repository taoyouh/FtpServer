// <copyright file="Program.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Net;
using System.Threading;

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
            Console.WriteLine("Welcome to demo of FTP server!");

            string baseDir = string.Empty;
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 21);
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-d":
                        try
                        {
                            baseDir = args[++i];
                        }
                        catch
                        {
                            Console.WriteLine("Directory incorrect");
                            goto help;
                        }
                        break;
                    case "-i":
                        try
                        {
                            ep.Address = IPAddress.Parse(args[++i]);
                        }
                        catch
                        {
                            Console.WriteLine("IP incorrect");
                            goto help;
                        }
                        break;
                    case "-p":
                        try
                        {
                            ep.Port = int.Parse(args[++i]);
                        }
                        catch
                        {
                            Console.WriteLine("Port incorrect");
                            goto help;
                        }
                        break;
                    case "?":
                    case "help":
                    case "-h":
                    case "--help":
                        help:
                        Console.WriteLine("Usage: -d {Root Directory} [-i {Listening IP} [-p {Listening Port}]");
                        return;
                }
            }

            if (string.IsNullOrWhiteSpace(baseDir))
            {
                Console.WriteLine("Please input base directory");
                baseDir = Console.ReadLine();
            }

            var server = new FtpServer(ep, baseDir);

            var cancelSource = new CancellationTokenSource();
            var runResult = server.RunAsync(cancelSource.Token);

            Console.WriteLine("FTP server has been started.");
            Console.WriteLine("Listening at: {0}", ep);
            Console.WriteLine("Root dir: {0}", baseDir);
            while (true)
            {
                var command = Console.ReadLine();
                if (command.ToUpper() == "QUIT")
                {
                    cancelSource.Cancel();
                    runResult.Wait();
                    Console.WriteLine("Quited.");
                    return;
                }
            }
        }
    }
}
