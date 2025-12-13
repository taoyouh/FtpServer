// <copyright file="SocketExtensionsTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.Net;
using System.Net.Sockets;
using Zhaobang.FtpServer.Connections;

namespace Zhaobang.FtpServer.Tests.Connections
{
    /// <summary>
    /// Tests for the <see cref="SocketExtensions"/> class.
    /// </summary>
    [TestClass]
    public class SocketExtensionsTests
    {
        /// <summary>
        /// The cancellation token should cancel the in-progress asynchronous <see cref="SocketExtensions.AcceptAsync(Socket, CancellationToken)"/> call.
        /// </summary>
        /// <returns>The <see cref="Task"/> representing the asynchronous operation.</returns>
        [TestMethod]
        public async Task AcceptSocketCancelTestAsync()
        {
            using Socket listenSocket = new(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.IPv6Loopback, 0));
            listenSocket.Listen();

            CancellationTokenSource cancellation = new();
            Task<Socket> acceptTask = SocketExtensions.AcceptAsync(listenSocket, cancellation.Token);

            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await acceptTask);
        }

        /// <summary>
        /// <see cref="SocketExtensions.AcceptAsync(Socket, CancellationToken)"/> should accept incoming connection.
        /// </summary>
        /// <returns>The <see cref="Task"/> representing the asynchronous operation.</returns>
        [TestMethod]
        public async Task AcceptSocketTestAsync()
        {
            using Socket listenSocket = new(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.IPv6Loopback, 0));
            listenSocket.Listen();

            EndPoint? listenEndPoint = listenSocket.LocalEndPoint;
            Assert.IsNotNull(listenEndPoint);

            CancellationTokenSource cancellation = new();
            Task<Socket> acceptTask = SocketExtensions.AcceptAsync(listenSocket, cancellation.Token);

            using Socket connectSocket = new(SocketType.Stream, ProtocolType.Tcp);
            await connectSocket.ConnectAsync(listenEndPoint);

            using Socket acceptSocket = await acceptTask;
            Memory<byte> sendBuffer = new byte[1];
            Memory<byte> receiveBuffer = new byte[1];
            Assert.AreEqual(1, await connectSocket.SendAsync(sendBuffer));
            Assert.AreEqual(1, await acceptSocket.ReceiveAsync(receiveBuffer));
        }

        /// <summary>
        /// Multiple calls to <see cref="SocketExtensions.AcceptAsync(Socket, CancellationToken)"/> should not block each other.
        /// </summary>
        /// <returns>The <see cref="Task"/> representing the asynchronous operation.</returns>
        [TestMethod]
        public async Task ConcurrentAcceptTestAsync()
        {
            using Socket listenSocket1 = new(SocketType.Stream, ProtocolType.Tcp);
            listenSocket1.Bind(new IPEndPoint(IPAddress.IPv6Loopback, 0));
            listenSocket1.Listen();

            var listenEndPoint1 = listenSocket1.LocalEndPoint as IPEndPoint;
            Assert.IsNotNull(listenEndPoint1);

            using Socket listenSocket2 = new(SocketType.Stream, ProtocolType.Tcp);
            listenSocket2.Bind(new IPEndPoint(IPAddress.IPv6Loopback, 0));
            listenSocket2.Listen();

            var listenEndPoint2 = listenSocket2.LocalEndPoint as IPEndPoint;
            Assert.IsNotNull(listenEndPoint2);

            Task t1 = SocketExtensions.AcceptAsync(listenSocket1, CancellationToken.None);
            Task t2 = SocketExtensions.AcceptAsync(listenSocket2, CancellationToken.None);

            using TcpClient client2 = new();
            await client2.ConnectAsync(listenEndPoint2);
            await t2;

            using TcpClient client1 = new();
            await client1.ConnectAsync(listenEndPoint1);
            await t1;
        }
    }
}