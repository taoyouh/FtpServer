// <copyright file="ActiveTcpConnection.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// Represents a TCP connection raised by FTP server.
    /// </summary>
    internal sealed class ActiveTcpConnection : ITcpConnection
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActiveTcpConnection"/> class.
        /// </summary>
        /// <param name="tcpClient">The already-connected TCP client.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tcpClient"/> is null.</exception>
        public ActiveTcpConnection(TcpClient tcpClient)
        {
            this.client = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            this.stream = tcpClient.GetStream();
            Debug.Assert(this.client.Connected, $"{nameof(tcpClient)} should be connected before passing into {nameof(ActiveTcpConnection)}.");
        }

        /// <inheritdoc/>
        public NetworkStream Stream
        {
            get => this.stream;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.stream.Dispose();
            this.client.Dispose();
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        /// <exception cref="InvalidOperationException">Always throws this exception.</exception>
        public Task WaitForClientAsync()
        {
            throw new InvalidOperationException();
        }
    }
}
