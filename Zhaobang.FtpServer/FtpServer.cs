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
using Zhaobang.FtpServer.Authenticate;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.File;
using Zhaobang.FtpServer.Trace;

namespace Zhaobang.FtpServer
{
    /// <summary>
    /// The class to run an FTP server.
    /// </summary>
    public sealed class FtpServer : IControlConnectionHost
    {
        private readonly IDataConnectionFactory dataConnFactory;
        private readonly IAuthenticator authenticator;
        private readonly IFileProviderFactory fileProviderFactory;
        private readonly IControlConnectionSslFactory controlConnectionSslFactory;
        private readonly FtpTracer tracer = new FtpTracer();

        private TcpListener tcpListener;

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpServer"/> class
        /// with <see cref="SimpleFileProviderFactory"/>, <see cref="LocalDataConnectionFactory"/>,
        /// and <see cref="AnonymousAuthenticator"/>.
        /// </summary>
        /// <param name="endPoint">The local end point to listen, usually 0.0.0.0:21.</param>
        /// <param name="baseDirectory">The directory to provide files.</param>
        public FtpServer(IPEndPoint endPoint, string baseDirectory)
            : this(endPoint, new SimpleFileProviderFactory(baseDirectory), new LocalDataConnectionFactory(), new AnonymousAuthenticator())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpServer"/> class.
        /// The server uses custom file, data connection, and authentication provider.
        /// </summary>
        /// <param name="endPoint">The local end point to listen, usually 0.0.0.0:21.</param>
        /// <param name="fileProviderFactory">The <see cref="IFileProviderFactory"/> to use.</param>
        /// <param name="dataConnFactory">The <see cref="IDataConnectionFactory"/> to use.</param>
        /// <param name="authenticator">The <see cref="IAuthenticator"/> to use.</param>
        public FtpServer(
            IPEndPoint endPoint,
            IFileProviderFactory fileProviderFactory,
            IDataConnectionFactory dataConnFactory,
            IAuthenticator authenticator)
            : this(endPoint, fileProviderFactory, dataConnFactory, authenticator, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpServer"/> class.
        /// The server uses custom file, data connection, and authentication, and control connection SSL provider.
        /// </summary>
        /// <param name="endPoint">The local end point to listen, usually 0.0.0.0:21.</param>
        /// <param name="fileProviderFactory">The <see cref="IFileProviderFactory"/> to use.</param>
        /// <param name="dataConnFactory">The <see cref="IDataConnectionFactory"/> to use.</param>
        /// <param name="authenticator">The <see cref="IAuthenticator"/> to use.</param>
        /// <param name="controlConnectionSslFactory">The <see cref="IControlConnectionSslFactory"/> to upgrade control connection to SSL.</param>
        public FtpServer(
            IPEndPoint endPoint,
            IFileProviderFactory fileProviderFactory,
            IDataConnectionFactory dataConnFactory,
            IAuthenticator authenticator,
            IControlConnectionSslFactory controlConnectionSslFactory)
        {
            tcpListener = new TcpListener(endPoint);

            this.fileProviderFactory = fileProviderFactory;
            this.dataConnFactory = dataConnFactory;
            this.authenticator = authenticator;
            this.controlConnectionSslFactory = controlConnectionSslFactory;

            tracer.CommandInvoked += Tracer_CommandInvoked;
            tracer.ReplyInvoked += Tracer_ReplyInvoked;
        }

        /// <inheritdoc/>
        public FtpTracer Tracer => tracer;

        /// <inheritdoc/>
        IDataConnectionFactory IControlConnectionHost.DataConnector { get => dataConnFactory; }

        /// <inheritdoc/>
        IAuthenticator IControlConnectionHost.Authenticator { get => authenticator; }

        /// <inheritdoc/>
        IFileProviderFactory IControlConnectionHost.FileManager { get => fileProviderFactory; }

        /// <inheritdoc/>
        IControlConnectionSslFactory IControlConnectionHost.ControlConnectionSslFactory => controlConnectionSslFactory;

        /// <summary>
        /// Gets the local end point the server is listening on.
        /// </summary>
        internal IPEndPoint EndPoint => tcpListener.LocalEndpoint as IPEndPoint;

        /// <summary>
        /// Start the FTP server.
        /// </summary>
        /// <param name="cancellationToken">Token to stop the FTP server.</param>
        /// <returns>The task that waits until the server stops.</returns>
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

                    async Task Run()
                    {
                        try
                        {
                            using (var handler = new ControlConnection(
                                this,
                                tcpClient.GetStream(),
                                tcpClient.Client.RemoteEndPoint as IPEndPoint,
                                tcpClient.Client.LocalEndPoint as IPEndPoint))
                            {
                                await handler.RunAsync(cancellationToken);
                            }
                        }
                        finally
                        {
                            tcpClient.Dispose();
                        }
                    }
                    _ = Run();
                }
            }
            finally
            {
                tcpListener.Stop();
            }
        }

        private static void Tracer_ReplyInvoked(string replyCode, IPEndPoint remoteAddress)
        {
            System.Diagnostics.Debug.WriteLine($"{remoteAddress}, reply, {replyCode}");
        }

        private static void Tracer_CommandInvoked(string command, IPEndPoint remoteAddress)
        {
            System.Diagnostics.Debug.WriteLine($"{remoteAddress}, command, {command}");
        }
    }
}
