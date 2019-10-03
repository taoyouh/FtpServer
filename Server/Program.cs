// <copyright file="Program.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Zhaobang.FtpServer.Authenticate;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.File;

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
            List<FtpServer> servers = new List<FtpServer>();

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

            X509Certificate certificate = null;
            try
            {
                if (!string.IsNullOrEmpty(config.CertificatePath))
                {
                    certificate = new X509Certificate(config.CertificatePath, config.CertificatePassword);
                    config.CertificatePassword = null;
                }

                var cancelSource = new CancellationTokenSource();
                var runResults = config.EndPoints.Select(
                    ep =>
                    {
                        var fileProviderFactory = new SimpleFileProviderFactory(config.BaseDirectory);
                        var dataConnectionFactory =
                            certificate == null ? new LocalDataConnectionFactory() : (IDataConnectionFactory)new SslLocalDataConnectionFactory(certificate);
                        var authenticator = new AnonymousAuthenticator();
                        var controlConnectionSslFactory =
                            certificate == null ? null : new ControlConnectionSslFactory(certificate);
                        var server = new FtpServer(ep, fileProviderFactory, dataConnectionFactory, authenticator, controlConnectionSslFactory);

                        servers.Add(server);
                        return server.RunAsync(cancelSource.Token)
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
                        });
                    })
                    .ToArray();

                Console.WriteLine("FTP server has been started.");
                Console.WriteLine("Config file: \n\t{0}", configFileInfo.FullName);
                Console.WriteLine("Root dir: \n\t{0}", config.BaseDirectory);
                Console.WriteLine(
                    "End points:\n\t{0}",
                    string.Join("\n\t", config.EndPoints.Select(ep => ep.ToString())));
                Console.WriteLine("Use command \"quit\" to stop FTP server.");
                Console.WriteLine("Use command \"users\" to list connected users.");

                while (true)
                {
                    var command = Console.ReadLine();
                    if (command.ToUpper(CultureInfo.InvariantCulture) == "QUIT")
                    {
                        cancelSource.Cancel();
                        Console.WriteLine("Stopped accepting new connections. Waiting until all clients quit.");
                        Task.WaitAll(runResults);
                        Console.WriteLine("Quited.");
                        return;
                    }
                    else if (command.ToUpper(CultureInfo.InvariantCulture) == "USERS")
                    {
                        Console.WriteLine("Connected users:");
                        foreach (var server in servers)
                        {
                            lock (server.Tracer.ConnectedUsersSyncRoot)
                            {
                                foreach (var user in server.Tracer.ConnectedUsersView)
                                {
                                    Console.WriteLine(user.ToString());
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                certificate.Dispose();
            }
        }
    }
}
