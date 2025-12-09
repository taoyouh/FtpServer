// <copyright file="LocalDataConnectionTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Zhaobang.FtpServer.Connections;

namespace Zhaobang.FtpServer.Tests.Connections
{
    /// <summary>
    /// Tests for <see cref="LocalDataConnection"/>.
    /// </summary>
    [TestClass]
    public class LocalDataConnectionTests
    {
        private readonly TestContext testContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDataConnectionTests"/> class.
        /// </summary>
        /// <param name="testContext">The context of test operation used to cancel the test.</param>
        public LocalDataConnectionTests(TestContext testContext)
        {
            ArgumentNullException.ThrowIfNull(testContext);
            this.testContext = testContext;
        }

        /// <summary>
        /// When a client connects to the listening port of <see cref="LocalDataConnection"/>, its <see cref="LocalDataConnection.IsOpen"/> should be true.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ListenTestAsync()
        {
            var serverIp = IPAddress.IPv6Loopback;
            LocalDataConnection connection = new(serverIp);
            IPEndPoint listenEndPoint = connection.Listen();

            using TcpClient tcpClient = new();
            await tcpClient.ConnectAsync(listenEndPoint, this.testContext.CancellationToken);
            await this.WaitUntilOpenAsync(connection);

            Assert.IsTrue(connection.IsOpen);
            await connection.AcceptAsync();
        }

        /// <summary>
        /// <see cref="LocalDataConnection.AcceptAsync"/> should wait until a client connects to the listening end point.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task AcceptAsyncBeforeConnectionTestAsync()
        {
            var serverIp = IPAddress.IPv6Loopback;
            LocalDataConnection connection = new(serverIp);
            IPEndPoint listenEndPoint = connection.Listen();
            Task acceptTask = connection.AcceptAsync();

            Assert.IsFalse(connection.IsOpen);
            Assert.IsFalse(acceptTask.IsCompleted);

            using TcpClient tcpClient = new();
            await tcpClient.ConnectAsync(listenEndPoint, this.testContext.CancellationToken);
            await this.WaitUntilOpenAsync(connection);

            Assert.IsTrue(connection.IsOpen);
            await acceptTask;
        }

        /// <summary>
        /// <see cref="LocalDataConnection.AcceptAsync"/> should only be called when it's waiting for a incoming connection.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task AcceptWithoutListenTestAsync()
        {
            var serverIp = IPAddress.IPv6Loopback;
            LocalDataConnection connection = new(serverIp);

            await Assert.ThrowsAsync<InvalidOperationException>(connection.AcceptAsync);
        }

        /// <summary>
        /// When calling <see cref="LocalDataConnection.Listen"/>, previous <see cref="LocalDataConnection.AcceptAsync"/> calls should fail, and previous listening port should be closed.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ListenTwiceTestAsync()
        {
            var serverIp = IPAddress.IPv6Loopback;
            LocalDataConnection connection = new(serverIp);
            IPEndPoint endPoint1 = connection.Listen();
            Task acceptTask1 = connection.AcceptAsync();

            Assert.IsFalse(acceptTask1.IsCompleted);

            IPEndPoint endPoint2 = connection.Listen();
            Task acceptTask2 = connection.AcceptAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await acceptTask1);
            Assert.AreNotEqual(endPoint1, endPoint2);

            using TcpClient client1 = new();
            await Assert.ThrowsAsync<SocketException>(async () =>
                await client1.ConnectAsync(endPoint1, this.testContext.CancellationToken));
            using TcpClient client2 = new();
            await client2.ConnectAsync(endPoint2, this.testContext.CancellationToken);
            await this.WaitUntilOpenAsync(connection);

            Assert.IsTrue(connection.IsOpen);
            await acceptTask2;
        }

        /// <summary>
        /// Calling <see cref="LocalDataConnection.Listen"/> should listen for new connection and disconnect existing data connection.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ListenAfterSuccessfulConnectionTestAsync()
        {
            var serverIp = IPAddress.IPv6Loopback;
            LocalDataConnection connection = new(serverIp);
            IPEndPoint endPoint1 = connection.Listen();

            using TcpClient client1 = new();
            await client1.ConnectAsync(endPoint1, this.testContext.CancellationToken);

            IPEndPoint endPoint2 = connection.Listen();
            Task acceptTask2 = connection.AcceptAsync();

            await this.CheckTcpConnectionClosedAsync(client1);

            using TcpClient client2 = new();
            await client2.ConnectAsync(endPoint2, this.testContext.CancellationToken);
            await this.WaitUntilOpenAsync(connection);

            Assert.IsTrue(connection.IsOpen);
            await acceptTask2;
        }

        /// <summary>
        /// Closing <see cref="LocalDataConnection"/> should close its TCP connection.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task CloseTestAsync()
        {
            var serverIp = IPAddress.IPv6Loopback;
            LocalDataConnection connection = new(serverIp);
            IPEndPoint listenEndPoint = connection.Listen();

            using TcpClient tcpClient = new();
            await tcpClient.ConnectAsync(listenEndPoint, this.testContext.CancellationToken);
            await connection.AcceptAsync();
            await connection.DisconnectAsync();

            Memory<byte> buffer = new byte[1];
            Assert.AreEqual(0, await tcpClient.Client.ReceiveAsync(buffer, this.testContext.CancellationToken));
        }

        /// <summary>
        /// When calling <see cref="LocalDataConnection.ConnectActiveAsync"/>, previous <see cref="LocalDataConnection.AcceptAsync"/> calls should fail, and previous listening port should be closed.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ConnectAfterListenTestAsync()
        {
            var serverIp = IPAddress.IPv6Loopback;
            LocalDataConnection connection = new(serverIp);

            IPEndPoint endPoint1 = connection.Listen();
            Task acceptTask1 = connection.AcceptAsync();

            TcpListener listener = new(IPAddress.IPv6Loopback, 0);
            listener.Start();
            await connection.ConnectActiveAsync(IPAddress.IPv6Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, 2);

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await acceptTask1);
            await Assert.ThrowsAsync<InvalidOperationException>(connection.AcceptAsync);

            using TcpClient client1 = new();
            await Assert.ThrowsAsync<SocketException>(async () =>
                await client1.ConnectAsync(endPoint1, this.testContext.CancellationToken));
        }

        private async Task CheckTcpConnectionClosedAsync(TcpClient client)
        {
            Memory<byte> buffer = new byte[1];
            try
            {
                Assert.AreEqual(0, await client.Client.ReceiveAsync(buffer, this.testContext.CancellationToken));
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.ConnectionReset)
                {
                    throw;
                }
            }
        }

        private async Task WaitUntilOpenAsync(LocalDataConnection connection)
        {
            while (!connection.IsOpen)
            {
                this.testContext.CancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }
    }
}
