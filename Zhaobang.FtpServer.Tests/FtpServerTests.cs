// <copyright file="FtpServerTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Zhaobang.FtpServer.Authenticate;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.File;

namespace Zhaobang.FtpServer.Tests
{
    /// <summary>
    /// Tests for <see cref="FtpServer"/>.
    /// </summary>
    [TestClass]
    public sealed class FtpServerTests : IAsyncDisposable
    {
        private readonly TestContext testContext;
        private readonly CancellationTokenSource serverRunCts = new();
        private FtpServer? server;
        private Task? serverRunTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpServerTests"/> class.
        /// </summary>
        /// <param name="testContext">The context of test opration used to cancel the test.</param>
        public FtpServerTests(TestContext testContext)
        {
            ArgumentNullException.ThrowIfNull(testContext);
            this.testContext = testContext;
        }

        /// <summary>
        /// When client sends QUIT command, the server closes the connection.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ClientQuitTestAsync()
        {
            IPEndPoint endPoint = this.StartFtpServer();
            using TcpClient client = new();
            await client.ConnectAsync(endPoint, this.testContext.CancellationToken);
            using NetworkStream clientStream = client.GetStream();
            using StreamWriter writer = new(clientStream)
            {
                NewLine = "\r\n",
            };
            using StreamReader reader = new(clientStream);
            Assert.IsTrue((await reader.ReadLineAsync(this.testContext.CancellationToken))?.StartsWith("220 ", StringComparison.Ordinal));
            await writer.WriteLineAsync("QUIT".AsMemory(), this.testContext.CancellationToken);
            await writer.FlushAsync(this.testContext.CancellationToken);
            Assert.IsNull(reader.ReadLine());
            return;
        }

        /// <summary>
        /// Two clients connect to the server.
        /// </summary>
        /// /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ConcurrentClinetTestAsync()
        {
            IPEndPoint endPoint = this.StartFtpServer();
            using TcpClient client1 = new();
            using TcpClient client2 = new();
            await client1.ConnectAsync(endPoint, this.testContext.CancellationToken);
            await client2.ConnectAsync(endPoint, this.testContext.CancellationToken);
            using NetworkStream clientStream1 = client1.GetStream();
            using NetworkStream clientStream2 = client2.GetStream();
            using StreamWriter writer1 = new(clientStream1)
            {
                NewLine = "\r\n",
            };
            using StreamReader reader1 = new(clientStream1);
            using StreamWriter writer2 = new(clientStream2)
            {
                NewLine = "\r\n",
            };
            using StreamReader reader2 = new(clientStream2);
            Assert.IsTrue((await reader1.ReadLineAsync(this.testContext.CancellationToken))?.StartsWith("220 ", StringComparison.Ordinal));
            Assert.IsTrue((await reader2.ReadLineAsync(this.testContext.CancellationToken))?.StartsWith("220 ", StringComparison.Ordinal));
            await writer1.WriteLineAsync("QUIT".AsMemory(), this.testContext.CancellationToken);
            await writer1.FlushAsync(this.testContext.CancellationToken);
            await writer2.WriteLineAsync("QUIT".AsMemory(), this.testContext.CancellationToken);
            await writer2.FlushAsync(this.testContext.CancellationToken);
            Assert.IsNull(reader1.ReadLine());
            Assert.IsNull(reader2.ReadLine());
            return;
        }

        /// <summary>
        /// When server is started with IPv4 loopback address, the socket should use InterNetwork address family.
        /// </summary>
        [TestMethod]
        public void IPv4EndpointShouldUseInterNetworkAddressFamily()
        {
            IPEndPoint serverEndPoint = new(IPAddress.Loopback, 0);
            this.server = new(
                serverEndPoint,
                new MockFileProviderFactory(),
                new LocalDataConnectionFactory(),
                new AnonymousAuthenticator());

            this.serverRunTask = this.server.RunAsync(this.serverRunCts.Token);
            IPEndPoint actualEndPoint = this.server.EndPoint;

            Assert.AreEqual(AddressFamily.InterNetwork, actualEndPoint.AddressFamily);
            Assert.IsFalse(actualEndPoint.Address.IsIPv4MappedToIPv6);
        }

        /// <summary>
        /// When server is started with IPv6 loopback address, the socket should use InterNetworkV6 address family.
        /// </summary>
        [TestMethod]
        public void IPv6EndpointShouldUseInterNetworkV6AddressFamily()
        {
            IPEndPoint serverEndPoint = new(IPAddress.IPv6Loopback, 0);
            this.server = new(
                serverEndPoint,
                new MockFileProviderFactory(),
                new LocalDataConnectionFactory(),
                new AnonymousAuthenticator());

            this.serverRunTask = this.server.RunAsync(this.serverRunCts.Token);
            IPEndPoint actualEndPoint = this.server.EndPoint;

            Assert.AreEqual(AddressFamily.InterNetworkV6, actualEndPoint.AddressFamily);
        }

        /// <summary>
        /// Client can connect to server started with IPv4 endpoint.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task IPv4ServerAcceptIPv4Connection()
        {
            IPEndPoint serverEndPoint = new(IPAddress.Loopback, 0);
            this.server = new(
                serverEndPoint,
                new MockFileProviderFactory(),
                new LocalDataConnectionFactory(),
                new AnonymousAuthenticator());

            this.serverRunTask = this.server.RunAsync(this.serverRunCts.Token);

            using TcpClient client = new(AddressFamily.InterNetwork);
            await client.ConnectAsync(this.server.EndPoint, this.testContext.CancellationToken);
            using NetworkStream clientStream = client.GetStream();
            using StreamReader reader = new(clientStream);

            string? response = await reader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("220 ", StringComparison.Ordinal));
        }

        /// <summary>
        /// Client can connect to server started with IPv6 endpoint.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task IPv6ServerAcceptIPv6Connection()
        {
            IPEndPoint serverEndPoint = new(IPAddress.IPv6Loopback, 0);
            this.server = new(
                serverEndPoint,
                new MockFileProviderFactory(),
                new LocalDataConnectionFactory(),
                new AnonymousAuthenticator());

            this.serverRunTask = this.server.RunAsync(this.serverRunCts.Token);

            using TcpClient client = new(AddressFamily.InterNetworkV6);
            await client.ConnectAsync(this.server.EndPoint, this.testContext.CancellationToken);
            using NetworkStream clientStream = client.GetStream();
            using StreamReader reader = new(clientStream);

            string? response = await reader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("220 ", StringComparison.Ordinal));
        }

        /// <summary>
        /// When server is started with IPv4-mapped IPv6 address, it should throw ArgumentException.
        /// </summary>
        [TestMethod]
        public void IPv4MappedIPv6AddressShouldThrowArgumentException()
        {
            IPAddress ipv4MappedIPv6 = IPAddress.Loopback.MapToIPv6();

            IPEndPoint serverEndPoint = new(ipv4MappedIPv6, 0);

            // Creating FtpServer with IPv4-mapped IPv6 address should throw ArgumentException
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
                new FtpServer(
                    serverEndPoint,
                    new MockFileProviderFactory(),
                    new LocalDataConnectionFactory(),
                    new AnonymousAuthenticator()));

            Assert.Contains("IPv4-mapped IPv6", ex.Message);
        }

        /// <summary>
        /// FTP server on IPv4 can establish control connection and passive data connection using PASV command.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task IPv4ServerEstablishesControlAndPassiveDataConnection()
        {
            IPEndPoint serverEndPoint = new(IPAddress.Loopback, 0);
            this.server = new(
                serverEndPoint,
                new MockFileProviderFactory(),
                new LocalDataConnectionFactory(),
                new AnonymousAuthenticator());

            this.serverRunTask = this.server.RunAsync(this.serverRunCts.Token);

            using TcpClient controlClient = new(AddressFamily.InterNetwork);
            await controlClient.ConnectAsync(this.server.EndPoint, this.testContext.CancellationToken);
            using NetworkStream controlStream = controlClient.GetStream();
            using StreamReader controlReader = new(controlStream);
            using StreamWriter controlWriter = new(controlStream) { NewLine = "\r\n" };

            string? response = await controlReader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("220 ", StringComparison.Ordinal));

            await controlWriter.WriteLineAsync("PASV".AsMemory(), this.testContext.CancellationToken);
            await controlWriter.FlushAsync(this.testContext.CancellationToken);
            response = await controlReader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("227 ", StringComparison.Ordinal));

            // Format: 227 Entering Passive Mode (ip1,ip2,ip3,ip4,portHi,portLo)
            int openParen = response.IndexOf('(');
            int closeParen = response.IndexOf(')');
            string pasvData = response.Substring(openParen + 1, closeParen - openParen - 1);
            string[] parts = pasvData.Split(',');
            byte[] ipBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                ipBytes[i] = byte.Parse(parts[i], CultureInfo.InvariantCulture);
            }

            int portHi = int.Parse(parts[4], CultureInfo.InvariantCulture);
            int portLo = int.Parse(parts[5], CultureInfo.InvariantCulture);
            int dataPort = (portHi << 8) | portLo;
            IPAddress dataIp = new IPAddress(ipBytes);

            using TcpClient dataClient = new(AddressFamily.InterNetwork);
            await dataClient.ConnectAsync(dataIp, dataPort, this.testContext.CancellationToken);
            using NetworkStream dataStream = dataClient.GetStream();

            Assert.IsTrue(dataClient.Connected);

            await controlWriter.WriteLineAsync("QUIT".AsMemory(), this.testContext.CancellationToken);
            await controlWriter.FlushAsync(this.testContext.CancellationToken);
        }

        /// <summary>
        /// FTP server on IPv6 can establish control connection and extended passive data connection using EPSV command.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task IPv6ServerEstablishesControlAndExtendedPassiveDataConnection()
        {
            IPEndPoint serverEndPoint = new(IPAddress.IPv6Loopback, 0);
            this.server = new(
                serverEndPoint,
                new MockFileProviderFactory(),
                new LocalDataConnectionFactory(),
                new AnonymousAuthenticator());

            this.serverRunTask = this.server.RunAsync(this.serverRunCts.Token);

            using TcpClient controlClient = new(AddressFamily.InterNetworkV6);
            await controlClient.ConnectAsync(this.server.EndPoint, this.testContext.CancellationToken);
            using NetworkStream controlStream = controlClient.GetStream();
            using StreamReader controlReader = new(controlStream);
            using StreamWriter controlWriter = new(controlStream) { NewLine = "\r\n" };

            string? response = await controlReader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("220 ", StringComparison.Ordinal));

            await controlWriter.WriteLineAsync("EPSV 2".AsMemory(), this.testContext.CancellationToken);
            await controlWriter.FlushAsync(this.testContext.CancellationToken);
            response = await controlReader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("229 ", StringComparison.Ordinal));

            // Format: 229 Entering extended passive mode. (|||port|)
            int openParen = response.IndexOf('(');
            int closeParen = response.IndexOf(')');
            string pasvData = response.Substring(openParen + 1, closeParen - openParen - 1);
            string[] parts = pasvData.Split('|');
            int dataPort = int.Parse(parts[3], CultureInfo.InvariantCulture);

            using TcpClient dataClient = new(AddressFamily.InterNetworkV6);
            await dataClient.ConnectAsync(IPAddress.IPv6Loopback, dataPort, this.testContext.CancellationToken);
            using NetworkStream dataStream = dataClient.GetStream();

            Assert.IsTrue(dataClient.Connected);

            await controlWriter.WriteLineAsync("QUIT".AsMemory(), this.testContext.CancellationToken);
            await controlWriter.FlushAsync(this.testContext.CancellationToken);
        }

        /// <summary>
        /// FTP server on IPv4 can establish control connection and active data connection using PORT command.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task IPv4ServerEstablishesControlAndActiveDataConnection()
        {
            IPEndPoint serverEndPoint = new(IPAddress.Loopback, 0);
            this.server = new(
                serverEndPoint,
                new MockFileProviderFactory(),
                new LocalDataConnectionFactory(),
                new AnonymousAuthenticator());

            this.serverRunTask = this.server.RunAsync(this.serverRunCts.Token);

            using TcpClient controlClient = new(AddressFamily.InterNetwork);
            await controlClient.ConnectAsync(this.server.EndPoint, this.testContext.CancellationToken);
            using NetworkStream controlStream = controlClient.GetStream();
            using StreamReader controlReader = new(controlStream);
            using StreamWriter controlWriter = new(controlStream) { NewLine = "\r\n" };

            string? response = await controlReader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("220 ", StringComparison.Ordinal));

            using TcpListener dataListener = new(IPAddress.Loopback, 0);
            dataListener.Start();
            IPEndPoint dataEndPoint = (IPEndPoint)dataListener.LocalEndpoint;
            byte[] ipBytes = dataEndPoint.Address.GetAddressBytes();
            int portHi = dataEndPoint.Port / 256;
            int portLo = dataEndPoint.Port % 256;

            string portCommand = $"PORT {ipBytes[0]},{ipBytes[1]},{ipBytes[2]},{ipBytes[3]},{portHi},{portLo}";
            await controlWriter.WriteLineAsync(portCommand.AsMemory(), this.testContext.CancellationToken);
            await controlWriter.FlushAsync(this.testContext.CancellationToken);
            response = await controlReader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("200 ", StringComparison.Ordinal));

            using TcpClient dataClient = await dataListener.AcceptTcpClientAsync(this.testContext.CancellationToken);

            Assert.IsTrue(dataClient.Connected);

            await controlWriter.WriteLineAsync("QUIT".AsMemory(), this.testContext.CancellationToken);
            await controlWriter.FlushAsync(this.testContext.CancellationToken);
        }

        /// <summary>
        /// FTP server on IPv6 can establish control connection and extended active data connection using EPRT command.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task IPv6ServerEstablishesControlAndExtendedActiveDataConnection()
        {
            IPEndPoint serverEndPoint = new(IPAddress.IPv6Loopback, 0);
            this.server = new(
                serverEndPoint,
                new MockFileProviderFactory(),
                new LocalDataConnectionFactory(),
                new AnonymousAuthenticator());

            this.serverRunTask = this.server.RunAsync(this.serverRunCts.Token);

            using TcpClient controlClient = new(AddressFamily.InterNetworkV6);
            await controlClient.ConnectAsync(this.server.EndPoint, this.testContext.CancellationToken);
            using NetworkStream controlStream = controlClient.GetStream();
            using StreamReader controlReader = new(controlStream);
            using StreamWriter controlWriter = new(controlStream) { NewLine = "\r\n" };

            string? response = await controlReader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("220 ", StringComparison.Ordinal));

            using TcpListener dataListener = new(IPAddress.IPv6Loopback, 0);
            dataListener.Start();
            IPEndPoint dataEndPoint = (IPEndPoint)dataListener.LocalEndpoint;
            int dataPort = dataEndPoint.Port;

            string eprtCommand = $"EPRT |2|{IPAddress.IPv6Loopback}|{dataPort}|";
            await controlWriter.WriteLineAsync(eprtCommand.AsMemory(), this.testContext.CancellationToken);
            await controlWriter.FlushAsync(this.testContext.CancellationToken);
            response = await controlReader.ReadLineAsync(this.testContext.CancellationToken);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.StartsWith("200 ", StringComparison.Ordinal));

            using TcpClient dataClient = await dataListener.AcceptTcpClientAsync(this.testContext.CancellationToken);

            Assert.IsTrue(dataClient.Connected);

            await controlWriter.WriteLineAsync("QUIT".AsMemory(), this.testContext.CancellationToken);
            await controlWriter.FlushAsync(this.testContext.CancellationToken);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            this.serverRunCts?.Cancel();

            if (this.serverRunTask != null)
            {
                await this.serverRunTask;
            }
        }

        private IPEndPoint StartFtpServer()
        {
            IPEndPoint serverEndPoint = new(IPAddress.IPv6Loopback, 0);
            this.server = new(
                serverEndPoint,
                new MockFileProviderFactory(),
                new LocalDataConnectionFactory(),
                new AnonymousAuthenticator());
            this.serverRunTask = this.server.RunAsync(this.serverRunCts.Token);
            return this.server.EndPoint;
        }

        private class MockFileProviderFactory : IFileProviderFactory
        {
            public IFileProvider GetProvider(string user)
            {
                throw new NotImplementedException();
            }
        }

        private class MockFileProvider : IFileProvider
        {
            public Task CreateDirectoryAsync(string path)
            {
                throw new NotImplementedException();
            }

            public Task<Stream> CreateFileForWriteAsync(string path)
            {
                throw new NotImplementedException();
            }

            public Task DeleteAsync(string path)
            {
                throw new NotImplementedException();
            }

            public Task DeleteDirectoryAsync(string path)
            {
                throw new NotImplementedException();
            }

            public Task<IEnumerable<FileSystemEntry>> GetListingAsync(string path)
            {
                throw new NotImplementedException();
            }

            public Task<IEnumerable<string>> GetNameListingAsync(string path)
            {
                throw new NotImplementedException();
            }

            public string GetWorkingDirectory()
            {
                throw new NotImplementedException();
            }

            public Task<Stream> OpenFileForReadAsync(string path)
            {
                throw new NotImplementedException();
            }

            public Task<Stream> OpenFileForWriteAsync(string path)
            {
                throw new NotImplementedException();
            }

            public Task RenameAsync(string fromPath, string toPath)
            {
                throw new NotImplementedException();
            }

            public bool SetWorkingDirectory(string path)
            {
                throw new NotImplementedException();
            }
        }
    }
}
