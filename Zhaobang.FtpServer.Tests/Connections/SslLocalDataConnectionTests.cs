// <copyright file="SslLocalDataConnectionTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.ComponentModel;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.Tests.Helpers;

namespace Zhaobang.FtpServer.Tests.Connections
{
    /// <summary>
    /// Tests for <see cref="SslLocalDataConnection"/>.
    /// </summary>
    [TestClass]
    public class SslLocalDataConnectionTests : DataConnectionTests<SslLocalDataConnection>
    {
        private readonly TestCertificate testCertificate = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SslLocalDataConnectionTests"/> class.
        /// </summary>
        /// <param name="testContext">The test context.</param>
        public SslLocalDataConnectionTests(TestContext testContext)
            : base(testContext)
        {
        }

        /// <summary>
        /// Tests that client shall be able to establish TLS connection.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ListenAndUpgradeToSslTestAsync()
        {
            using SslLocalDataConnection connection = new(IPAddress.Loopback, this.testCertificate.Certificate);
            IPEndPoint listenEndPoint = connection.Listen();
            using TcpClient client = new(listenEndPoint.AddressFamily);
            await client.ConnectAsync(listenEndPoint, this.TestContext.CancellationToken);
            await connection.AcceptAsync();

            using SslStream sslStream = new(client.GetStream(), false, this.testCertificate.ValidationCallback);
            await Task.WhenAll(
                connection.UpgradeToSslAsync(),
                sslStream.AuthenticateAsClientAsync(string.Empty));

            await connection.DisconnectAsync();
            Assert.AreEqual(0, await sslStream.ReadAsync(new byte[1], this.TestContext.CancellationToken));
        }

        /// <summary>
        /// Tests that the client can send file using the TLS connection.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ClientSendFileTestAsync()
        {
            using SslLocalDataConnection connection = new(IPAddress.Loopback, this.testCertificate.Certificate);
            IPEndPoint listenEndPoint = connection.Listen();
            using TcpClient client = new(listenEndPoint.AddressFamily);
            await client.ConnectAsync(listenEndPoint, this.TestContext.CancellationToken);
            await connection.AcceptAsync();
            using SslStream sslStream = new(client.GetStream(), false, this.testCertificate.ValidationCallback);
            await Task.WhenAll(
                connection.UpgradeToSslAsync(),
                sslStream.AuthenticateAsClientAsync(string.Empty));

            MemoryStream receivedData = new();
            Task receiveTask = connection.RecieveAsync(receivedData);

            byte[] dataToSend = new byte[1024];
            new Random().NextBytes(dataToSend);
            await sslStream.WriteAsync(dataToSend.AsMemory());
            await sslStream.ShutdownAsync();

            await receiveTask;
            CollectionAssert.AreEqual(dataToSend, receivedData.ToArray());
        }

        /// <summary>
        /// Tests that the server can send file using the TLS connection.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ServerSendFileTestAsync()
        {
            using SslLocalDataConnection connection = new(IPAddress.Loopback, this.testCertificate.Certificate);
            IPEndPoint listenEndPoint = connection.Listen();
            using TcpClient client = new(listenEndPoint.AddressFamily);
            await client.ConnectAsync(listenEndPoint, this.TestContext.CancellationToken);
            await connection.AcceptAsync();
            using SslStream sslStream = new(client.GetStream(), false, this.testCertificate.ValidationCallback);
            await Task.WhenAll(
                connection.UpgradeToSslAsync(),
                sslStream.AuthenticateAsClientAsync(string.Empty));

            byte[] dataToSend = new byte[1024];
            new Random().NextBytes(dataToSend);
            Task sendTask = connection.SendAsync(new MemoryStream(dataToSend));

            byte[] receivedData = new byte[dataToSend.Length];
            int totalRead = 0;
            while (totalRead < receivedData.Length)
            {
                int bytesRead = await sslStream.ReadAsync(receivedData.AsMemory(totalRead), this.TestContext.CancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
            }

            CollectionAssert.AreEqual(dataToSend, receivedData);

            await sendTask;
            await connection.DisconnectAsync();

            Assert.AreEqual(-1, sslStream.ReadByte());
        }

        /// <inheritdoc/>
        protected override SslLocalDataConnection CreateDataConnection(IPAddress serverIp)
        {
            return new SslLocalDataConnection(serverIp, this.testCertificate.Certificate);
        }
    }
}
