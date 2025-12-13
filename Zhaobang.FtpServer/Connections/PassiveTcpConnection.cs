// <copyright file="PassiveTcpConnection.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// Represents the TCP connection of FTP passive data connection.
    /// </summary>
    internal class PassiveTcpConnection : ITcpConnection
    {
        private readonly Task acceptTask;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private volatile NetworkStream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="PassiveTcpConnection"/> class.
        /// </summary>
        /// <param name="listenAddress">The address to bind the listener on.</param>
        public PassiveTcpConnection(IPAddress listenAddress)
        {
            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(listenAddress, 0));
            listenSocket.Listen(0);
            this.ListenEndPoint = (IPEndPoint)listenSocket.LocalEndPoint;
            this.acceptTask = AcceptAsync(listenSocket, this.cts.Token);
        }

        /// <inheritdoc/>
        public NetworkStream Stream
        {
            get => this.stream;
        }

        /// <summary>
        /// Gets end point listening on.
        /// </summary>
        public IPEndPoint ListenEndPoint
        {
            get; private set;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.cts.Cancel();
            this.acceptTask.ContinueWith(task =>
            {
                this.stream?.Dispose();
            });
        }

        /// <summary>
        /// Waits until a client connects to the listener, after which <see cref="Stream"/> will be non-null.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        public async Task WaitForClientAsync()
        {
            await acceptTask;
        }

        private async Task AcceptAsync(Socket listenSocket, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("The current listener is closed before any incoming connection.");
                }

                Socket connectSocket = await listenSocket.AcceptAsync(token);
                this.stream = new NetworkStream(connectSocket, true);
            }
            finally
            {
                listenSocket.Dispose();
            }
        }
    }
}
