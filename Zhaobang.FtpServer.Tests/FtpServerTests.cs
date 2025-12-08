// <copyright file="FtpServerTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
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
            using StreamWriter writer = new(clientStream);
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
            using StreamWriter writer1 = new(clientStream1);
            using StreamReader reader1 = new(clientStream1);
            using StreamWriter writer2 = new(clientStream2);
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
