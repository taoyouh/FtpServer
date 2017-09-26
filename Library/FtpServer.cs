// <copyright file="FtpServer.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.File;

namespace Zhaobang.FtpServer
{
    /// <summary>
    /// The class to run an FTP server
    /// </summary>
    public sealed class FtpServer
    {
        private DataConnector dataConnector;
        private FtpAuthenticator authenticator;
        private FileManager fileManager;

        private IPEndPoint endPoint;
        private TcpListener tcpListener;

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpServer"/> class.
        /// The server uses a default authenticator that says yes all the time,
        /// and provide file from a base directory to every users.
        /// </summary>
        /// <param name="endPoint">The local end point to listen, usually 0.0.0.0:21</param>
        /// <param name="baseDirectory">The directory to provide files</param>
        public FtpServer(IPEndPoint endPoint, string baseDirectory)
        {
            this.endPoint = endPoint;
            tcpListener = new TcpListener(endPoint);

            fileManager = new FileManager(baseDirectory);
            dataConnector = new DataConnector();
            authenticator = new FtpAuthenticator();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpServer"/> class.
        /// The server uses custom file, data connection, and authentication provider.
        /// </summary>
        /// <param name="endPoint">The local end point to listen, usually 0.0.0.0:21</param>
        /// <param name="fileManager">The <see cref="File.FileManager"/> to use</param>
        /// <param name="dataConnector">The <see cref="Connections.DataConnector"/> to use</param>
        /// <param name="authenticator">The <see cref="FtpAuthenticator"/> to use</param>
        public FtpServer(
            IPEndPoint endPoint,
            FileManager fileManager,
            DataConnector dataConnector,
            FtpAuthenticator authenticator)
        {
            this.endPoint = endPoint;
            tcpListener = new TcpListener(endPoint);

            this.fileManager = fileManager;
            this.dataConnector = dataConnector;
            this.authenticator = authenticator;
        }

        /// <summary>
        /// Gets the manager that provides <see cref="DataConnection"/> for each user
        /// </summary>
        internal DataConnector DataConnector { get => dataConnector; }

        /// <summary>
        /// Gets the manager that authenticates user
        /// </summary>
        internal FtpAuthenticator Authenticator { get => authenticator; }

        /// <summary>
        /// Gets the manager that provides <see cref="FileProvider"/> for each user
        /// </summary>
        internal FileManager FileManager { get => fileManager; }

        /// <summary>
        /// Start the FTP server
        /// </summary>
        /// <param name="cancellationToken">Token to stop the FTP server</param>
        /// <returns>The task that waits until the server stops</returns>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                tcpListener.Start();
                cancellationToken.Register(() => tcpListener.Stop());
                while (true)
                {
                    TcpClient tcpClient;
                    try
                    {
                        tcpClient = await tcpListener.AcceptTcpClientAsync().WithCancellation(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    try
                    {
                        ControlConnection handler = new ControlConnection(this, tcpClient);
                        var result = handler.RunAsync(cancellationToken);
                    }
                    catch (Exception)
                    {
                        tcpClient.Dispose();
                    }
                }
            }
            finally
            {
                tcpListener.Stop();
            }
        }
    }
}
