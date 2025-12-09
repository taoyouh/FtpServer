// <copyright file="ITcpConnection.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// The interface of the TCP connection used in FTP data connection.
    /// </summary>
    internal interface ITcpConnection : IDisposable
    {
        /// <summary>
        /// Gets the stream of the underlying connection or null if not connected.
        /// </summary>
        NetworkStream Stream { get; }

        /// <summary>
        /// If this is an passive connection, waits for client connection. Otherwise throws <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        /// <exception cref="InvalidOperationException">This is an active connection which never waits for client conneciton.</exception>
        Task WaitForClientAsync();
    }
}
