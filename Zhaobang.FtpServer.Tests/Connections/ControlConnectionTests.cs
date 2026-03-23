// <copyright file="ControlConnectionTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Moq;
using Zhaobang.FtpServer.Authenticate;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.File;
using Zhaobang.FtpServer.Tests.Helpers;
using Zhaobang.FtpServer.Trace;

namespace Zhaobang.FtpServer.Tests.Connections
{
    /// <summary>
    /// Tests for <see cref="ControlConnection"/>.
    /// </summary>
    [TestClass]
    public class ControlConnectionTests
    {
        private readonly TestContext testContext;
        private readonly MockControlConnectionHost mockControlConnectionHost = new();
        private readonly IPEndPoint serverEndPoint = new(IPAddress.IPv6Loopback, 56788);
        private readonly IPEndPoint clientEndPoint = new(IPAddress.IPv6Loopback, 56789);
        private readonly CombinedStream stream;
        private readonly TestCertificate testCertificate = new();

        private PipeWriter readPipeWriter;
        private PipeReader writePipeReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlConnectionTests"/> class.
        /// </summary>
        /// <param name="testContext">The context of test operation used to cancel the test.</param>
        public ControlConnectionTests(TestContext testContext)
        {
            ArgumentNullException.ThrowIfNull(testContext);
            this.testContext = testContext;
            (this.stream, this.readPipeWriter, this.writePipeReader) = NetworkStreamMock.Create();
        }

        /// <summary>
        /// Owl file on Android uses this sequence to get file list.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ListFileTestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            var runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            fileProvider.Setup(x => x.GetWorkingDirectory()).Returns("/");
            await this.WriteLineAsync("PWD"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().SequenceEqual("257 \"/\""u8));

            await this.WriteLineAsync("FEAT"u8.ToArray());
            List<(ReadOnlyMemory<byte> FeatName, ReadOnlyMemory<byte>? FeatParam)> features = await this.ReadFeaturesAsync();
            Assert.Contains(f => f.FeatName.Span.SequenceEqual("UTF8"u8) && f.FeatParam == null, features);

            await this.WriteLineAsync("OPTS UTF8 ON"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            using TcpClient dataClient = await this.ConnectWithEpsvAsync();

            await this.WriteLineAsync("TYPE A"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            FileSystemEntry[] files =
            [
                new() { Name = "图片.jpg" },
                new() { Name = "video.mp4" },
                new() { Name = "текст.txt" },
            ];
            fileProvider.Setup(x => x.GetListingAsync(string.Empty)).Returns(Task.FromResult(files.AsEnumerable()));
            await this.WriteLineAsync("LIST"u8.ToArray());
            byte[]? listResponse = await this.ReadLineAsync();
            Assert.IsTrue(listResponse.AsSpan().StartsWith("125 "u8) || listResponse.AsSpan().StartsWith("150 "u8));
            using NetworkStream dataStream = dataClient.GetStream();
            List<byte[]> listResult = await this.ReadLinesAsync(dataStream);

            Assert.HasCount(files.Length + 2, listResult);

            // RFC 2640: Paths are UTF-8
            UTF8Encoding encoding = new(false);
            Assert.IsTrue(listResult[0].All(x => x < 128));
            Assert.IsTrue(listResult[1].AsSpan().EndsWith(encoding.GetBytes(files[0].Name)));
            Assert.IsTrue(listResult[2].AsSpan().EndsWith(encoding.GetBytes(files[1].Name)));
            Assert.IsTrue(listResult[3].AsSpan().EndsWith(encoding.GetBytes(files[2].Name)));
            Assert.IsEmpty(listResult[4]);
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("226 "u8));

            await this.WriteLineAsync("PWD"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().SequenceEqual("257 \"/\""u8));

            await this.WriteLineAsync("QUIT"u8.ToArray());
            Assert.IsNull(await this.ReadLineAsync());

            await runTask;
        }

        /// <summary>
        /// Windows Explorer on Windows 11 25H2 uses this sequence to get file list.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task ListFileEmptyDirectoryTestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            var runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS IEUser@"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            await this.WriteLineAsync("opts utf8 on"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            fileProvider.Setup(x => x.GetWorkingDirectory()).Returns("/");
            await this.WriteLineAsync("PWD"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().SequenceEqual("257 \"/\""u8));

            await this.WriteLineAsync("TYPE A"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            using TcpClient dataClient = await this.ConnectWithEpsvAsync();

            FileSystemEntry[] files = [];
            fileProvider.Setup(x => x.GetListingAsync(string.Empty)).Returns(Task.FromResult(files.AsEnumerable()));
            await this.WriteLineAsync("LIST"u8.ToArray());
            byte[]? listResponse = await this.ReadLineAsync();
            Assert.IsTrue(listResponse.AsSpan().StartsWith("125 "u8) || listResponse.AsSpan().StartsWith("150 "u8));
            using NetworkStream dataStream = dataClient.GetStream();
            List<byte[]> listResult = await this.ReadLinesAsync(dataStream);

            Assert.HasCount(files.Length + 2, listResult);
            Assert.IsTrue(listResult[0].All(x => x < 128));

            // If only one CRLF is printed, Windows 11 25H2 considers it as error.
            Assert.IsNotEmpty(listResult[0]);
            Assert.IsEmpty(listResult[1]);
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("226 "u8));

            await this.WriteLineAsync("noop"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Test handling of MLsD, OPTS MLst and the corresponding FEAT command, based on RFC 3659.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task MLsDTestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IMLstFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            await this.WriteLineAsync("FEAT"u8.ToArray());
            {
                List<(ReadOnlyMemory<byte> FeatName, ReadOnlyMemory<byte>? FeatParam)> features = await this.ReadFeaturesAsync();
                (ReadOnlyMemory<byte> _, ReadOnlyMemory<byte>? mlstFeatParam) = features.Single(f => f.FeatName.Span.SequenceEqual("MLST"u8));
                Dictionary<string, bool> facts = ParseMLstFeatParams(mlstFeatParam);
                Assert.IsTrue(facts.ContainsKey("Size"));
                Assert.IsTrue(facts.ContainsKey("Type"));
                Assert.IsTrue(facts.ContainsKey("Perm"));
                Assert.IsTrue(facts.ContainsKey("Modify"));
            }

            await this.WriteLineAsync("OPTS MLST Size;Type;Perm;Modify;"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            await this.WriteLineAsync("FEAT"u8.ToArray());
            {
                List<(ReadOnlyMemory<byte> FeatName, ReadOnlyMemory<byte>? FeatParam)> features = await this.ReadFeaturesAsync();
                (ReadOnlyMemory<byte> _, ReadOnlyMemory<byte>? mlstFeatParam) = features.Single(f => f.FeatName.Span.SequenceEqual("MLST"u8));
                Dictionary<string, bool> facts = ParseMLstFeatParams(mlstFeatParam);
                Assert.IsTrue(facts["Size"]);
                Assert.IsTrue(facts["Type"]);
                Assert.IsTrue(facts["Perm"]);
                Assert.IsTrue(facts["Modify"]);
            }

            FileSystemEntry[] files =
            [
                new() { Name = "图片.jpg", Length = long.MaxValue, LastWriteTime = new DateTime(1000, 1, 1, 0, 0, 0, 0) },
                new() { Name = "video.mp4", Length = 1, IsDirectory = true, LastWriteTime = new DateTime(9999, 12, 31, 23, 59, 59, 999) },
                new() { Name = "текст.txt", Length = 15, LastWriteTime = new DateTime(2026, 1, 28, 21, 2, 1, 234) },
                new() { Name = "Folder", IsDirectory = true, LastWriteTime = new DateTime(2026, 1, 28, 21, 2, 2, 345) },
                new() { Name = "Read only folder", IsDirectory = true, IsReadOnly = true, LastWriteTime = new DateTime(2026, 1, 28, 21, 2, 3, 456) },
            ];
            fileProvider.Setup(x => x.GetWorkingDirectory()).Returns("/");
            fileProvider.Setup(x => x.GetChildItems(string.Empty)).Returns(Task.FromResult(files.AsEnumerable()));
            {
                using TcpClient dataClient = await this.ConnectWithEpsvAsync();
                using NetworkStream dataStream = dataClient.GetStream();

                await this.WriteLineAsync("MLsD"u8.ToArray());
                byte[]? mlsdResponse = await this.ReadLineAsync();
                Assert.IsTrue(mlsdResponse.AsSpan().StartsWith("125 "u8) || mlsdResponse.AsSpan().StartsWith("150 "u8));

                List<byte[]> listResult = await this.ReadLinesAsync(dataStream);
                Assert.HasCount(files.Length + 1, listResult);
                for (int i = 0; i < files.Length; ++i)
                {
                    (Dictionary<string, string> facts, string pathName) = ParseMlsXEntry(listResult[i]);
                    Assert.AreEqual(files[i].Name, pathName);
                    Assert.HasCount(4, facts); // Size, type, perm and modify
                    Assert.AreEqual(files[i].Length, long.Parse(facts["Size"], CultureInfo.InvariantCulture));
                    if (files[i].IsDirectory)
                    {
                        Assert.AreEqual("dir", facts["Type"]);

                        if (files[i].IsReadOnly)
                        {
                            Assert.AreEqual("defl", facts["Perm"]);
                        }
                        else
                        {
                            Assert.AreEqual("cdeflmp", facts["Perm"]);
                        }
                    }
                    else
                    {
                        Assert.AreEqual("file", facts["Type"]);

                        if (files[i].IsReadOnly)
                        {
                            Assert.AreEqual("dfr", facts["Perm"]);
                        }
                        else
                        {
                            Assert.AreEqual("adfrw", facts["Perm"]);
                        }
                    }
                }

                Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("226 "u8));
            }

            await this.WriteLineAsync("OPTS MLST"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            await this.WriteLineAsync("FEAT"u8.ToArray());
            {
                List<(ReadOnlyMemory<byte> FeatName, ReadOnlyMemory<byte>? FeatParam)> features = await this.ReadFeaturesAsync();
                (ReadOnlyMemory<byte> _, ReadOnlyMemory<byte>? mlstFeatParam) = features.Single(f => f.FeatName.Span.SequenceEqual("MLST"u8));
                Dictionary<string, bool> facts = ParseMLstFeatParams(mlstFeatParam);
                Assert.DoesNotContain(pair => pair.Value, facts);
            }

            {
                using TcpClient dataClient = await this.ConnectWithEpsvAsync();
                using NetworkStream dataStream = dataClient.GetStream();

                await this.WriteLineAsync("MLSD"u8.ToArray());
                byte[]? mlsdResponse = await this.ReadLineAsync();
                Assert.IsTrue(mlsdResponse.AsSpan().StartsWith("125 "u8) || mlsdResponse.AsSpan().StartsWith("150 "u8));

                List<byte[]> listResult = await this.ReadLinesAsync(dataStream);
                Assert.HasCount(files.Length + 1, listResult);
                for (int i = 0; i < files.Length; ++i)
                {
                    (Dictionary<string, string> facts, string pathName) = ParseMlsXEntry(listResult[i]);
                    Assert.AreEqual(files[i].Name, pathName);
                    Assert.IsEmpty(facts); // No facts should be returned
                }

                Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("226 "u8));
            }

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Test MLst command on a file and the corresponding OPTS and FEAT command (RFC 3659).
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task MLstTestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IMLstFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            await this.WriteLineAsync("OPTS MLST Size;Type;Perm;Modify;"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            FileSystemEntry file = new() { Name = "图片.jpg", Length = long.MaxValue, LastWriteTime = new DateTime(1000, 1, 1, 0, 0, 0, 0) };
            fileProvider.Setup(x => x.GetWorkingDirectory()).Returns("/");
            fileProvider.Setup(x => x.GetItemAsync("/图片.jpg")).Returns(Task.FromResult(file));

            await this.WriteLineAsync("MLst /图片.jpg"u8.ToArray());
            {
                Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("250-"u8));
                byte[]? line = await this.ReadLineAsync();
                Assert.IsNotNull(line);
                Assert.IsGreaterThan(0, line.Length);
                Assert.AreEqual(' ', (char)line[0]);
                (Dictionary<string, string>? facts, string? pathName) = ParseMlsXEntry(line.AsMemory()[1..]);
                Assert.AreEqual(file.Length, long.Parse(facts["Size"], CultureInfo.InvariantCulture));
                Assert.AreEqual("file", facts["Type"]);
                Assert.AreEqual("adfrw", facts["Perm"]);
                Assert.AreEqual(file.Name, pathName);

                Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("250 "u8));
            }

            await this.WriteLineAsync("OPTS MLST"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            await this.WriteLineAsync("MLST /图片.jpg"u8.ToArray());
            {
                Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("250-"u8));
                byte[]? line = await this.ReadLineAsync();
                Assert.IsNotNull(line);
                Assert.IsGreaterThan(0, line.Length);
                Assert.AreEqual(' ', (char)line[0]);
                (Dictionary<string, string>? facts, string? pathName) = ParseMlsXEntry(line.AsMemory()[1..]);
                Assert.IsEmpty(facts); // No facts should be returned
                Assert.AreEqual(file.Name, pathName);

                Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("250 "u8));
            }

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Tests that the MLST feature line is not returned when not supported.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task MLstFeatLineNotReturnedWhenNotSupportedTestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            await this.WriteLineAsync("FEAT"u8.ToArray());
            {
                List<(ReadOnlyMemory<byte> FeatName, ReadOnlyMemory<byte>? FeatParam)> features = await this.ReadFeaturesAsync();
                Assert.DoesNotContain(f => f.FeatName.Span.SequenceEqual("MLST"u8), features);
            }

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Test MLsD command with non-existent path should return 550 response (RFC 3659 7.2.1).
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task MLsDNonExistentPathReturns550TestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IMLstFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            // Setup GetChildItems to throw FileNoAccessException for non-existent path
            fileProvider.Setup(x => x.GetChildItems("/nonexistent"))
                .ThrowsAsync(new FileNoAccessException("Path not found"));

            await this.WriteLineAsync("MLSD /nonexistent"u8.ToArray());
            byte[]? response = await this.ReadLineAsync();
            Assert.IsTrue(response.AsSpan().StartsWith("550 "u8));

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Test MLsD command with file (not directory) path should return 501 response (RFC 3659 7.2.1).
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task MLsDFilePathReturns501TestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IMLstFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            // Setup GetChildItems to throw ArgumentException for file path
            fileProvider.Setup(x => x.GetChildItems("/file.txt"))
                .ThrowsAsync(new ArgumentException("Path is not a directory"));

            await this.WriteLineAsync("MLSD /file.txt"u8.ToArray());
            byte[]? response = await this.ReadLineAsync();
            Assert.IsTrue(response.AsSpan().StartsWith("501 "u8));

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Test MLst command with non-existent path should return 550 response (RFC 3659 7.2.1).
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task MLstNonExistentPathReturns550TestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IMLstFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            // Setup GetItemAsync to throw FileNoAccessException for non-existent path
            fileProvider.Setup(x => x.GetItemAsync("/nonexistent.txt"))
                .ThrowsAsync(new FileNoAccessException("File not found"));

            await this.WriteLineAsync("MLST /nonexistent.txt"u8.ToArray());
            byte[]? response = await this.ReadLineAsync();
            Assert.IsTrue(response.AsSpan().StartsWith("550 "u8));

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Test MLsD command without authentication should return 530 response (RFC 959 5.4; RFC 3659 7.2.1).
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task MLsDWithoutAuthReturns530TestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IMLstFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("MLSD"u8.ToArray());
            byte[]? response = await this.ReadLineAsync();
            Assert.IsTrue(response.AsSpan().StartsWith("530 "u8));

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Test MLst command without authentication should return 530 response (RFC 959 5.4; RFC 3659 7.2.1).
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task MLstWithoutAuthReturns530TestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Mock<IMLstFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("MLST /somefile.txt"u8.ToArray());
            byte[]? response = await this.ReadLineAsync();
            Assert.IsTrue(response.AsSpan().StartsWith("530 "u8));

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Test PASV command with IPv4 mapped IPv6 address.
        /// When IDataConnection.Listen() returns IPv4 mapped IPv6 address,
        /// PASV command should still work correctly.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task PasvWithIPv4MappedToIPv6AddressTestAsync()
        {
            var mockDataConnection = new Mock<IDataConnection>();
            var ipv4MappedIPv6 = IPAddress.Parse("::ffff:127.0.0.1");
            var ipv4MappedEndpoint = new IPEndPoint(ipv4MappedIPv6, 21234);
            mockDataConnection.Setup(x => x.Listen()).Returns(ipv4MappedEndpoint);

            Mock<IDataConnectionFactory> mockDataConnectionFactory = new Mock<IDataConnectionFactory>();
            mockDataConnectionFactory.Setup(x => x.GetDataConnection(It.IsAny<IPAddress>())).Returns(mockDataConnection.Object);

            Mock<IFileProvider> fileProvider = new();
            var mockHost = new MockControlConnectionHost();
            mockHost.DataConnector = mockDataConnectionFactory.Object;
            mockHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            using ControlConnection controlConnection = new(
                mockHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            await this.WriteLineAsync("PASV"u8.ToArray());
            byte[]? pasvResponse = await this.ReadLineAsync();
            Assert.IsNotNull(pasvResponse);
            Assert.IsTrue(pasvResponse.AsSpan().StartsWith("227 "u8));

            // Parse the PASV response to verify IP address and port format
            // Format: 227 ... (h1,h2,h3,h4,p1,p2)
            string responseString = System.Text.Encoding.UTF8.GetString(pasvResponse);
            int openParen = responseString.IndexOf('(');
            int closeParen = responseString.IndexOf(')');
            Assert.IsTrue(openParen >= 0 && closeParen > openParen);

            string[] addressParts = responseString[(openParen + 1)..closeParen].Split(',');
            Assert.HasCount(6, addressParts);

            // Verify the IP address is 127.0.0.1 (from IPv4 mapped IPv6 ::ffff:127.0.0.1)
            Assert.AreEqual("127", addressParts[0]);
            Assert.AreEqual("0", addressParts[1]);
            Assert.AreEqual("0", addressParts[2]);
            Assert.AreEqual("1", addressParts[3]);

            // Verify port is in valid range
            int p1 = int.Parse(addressParts[4], CultureInfo.InvariantCulture);
            int p2 = int.Parse(addressParts[5], CultureInfo.InvariantCulture);
            int port = (p1 * 256) + p2;
            Assert.AreEqual(21234, port);

            this.readPipeWriter.Complete();
            await runTask;
        }

        /// <summary>
        /// Tests that AUTH command without SSL factory returns 502 (Command not implemented). See RFC 2228 section 3.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task AuthWithoutSslFactoryTestAsync()
        {
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("AUTH TLS"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("502 "u8));

            await this.WriteLineAsync("QUIT"u8.ToArray());
            this.readPipeWriter.Complete();

            Assert.IsNull(await this.ReadLineAsync());
            await runTask;
        }

        /// <summary>
        /// Tests that AUTH command with invalid parameter returns 504 (Command not implemented for that parameter).
        /// See RFC 2228 section 3.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task AuthWithInvalidParameterTestAsync()
        {
            this.mockControlConnectionHost.
                ControlConnectionSslFactory = new ControlConnectionSslFactory(this.testCertificate.Certificate);
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("AUTH INVALID"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("504 "u8));

            await this.WriteLineAsync("QUIT"u8.ToArray());
            this.readPipeWriter.Complete();

            Assert.IsNull(await this.ReadLineAsync());
            await runTask;
        }

        /// <summary>
        /// Tests that control connection advertise supported commands as RFC 4217 section 6 in response to FEAT command.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task FeatWithTlsTestAsync()
        {
            this.mockControlConnectionHost.ControlConnectionSslFactory = new ControlConnectionSslFactory(this.testCertificate.Certificate);
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);

            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("FEAT"u8.ToArray());
            {
                List<(ReadOnlyMemory<byte> FeatName, ReadOnlyMemory<byte>? FeatParam)> features = await this.ReadFeaturesAsync();
                (ReadOnlyMemory<byte> featName, ReadOnlyMemory<byte>? featParam) = features.Single(f => f.FeatName.Span.SequenceEqual("AUTH"u8));
                Assert.IsNotNull(featParam);
                Assert.IsTrue(featParam.Value.Span.SequenceEqual("TLS"u8));
                Assert.Contains(f => f.FeatName.Span.SequenceEqual("PBSZ"u8), features);
                Assert.Contains(f => f.FeatName.Span.SequenceEqual("PROT"u8), features);
            }

            await this.WriteLineAsync("QUIT"u8.ToArray());
            this.readPipeWriter.Complete();

            Assert.IsNull(await this.ReadLineAsync());
            await runTask;
        }

        /// <summary>
        /// Tests that AUTH TLS command upgrades the connection as RFC 4217 12.1.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task AuthTlsUpgradeConnectionTestAsync()
        {
            this.mockControlConnectionHost.ControlConnectionSslFactory = new ControlConnectionSslFactory(this.testCertificate.Certificate);
            this.mockControlConnectionHost.DataConnector = new SslLocalDataConnectionFactory(this.testCertificate.Certificate);
            using ControlConnection controlConnection = new(
                this.mockControlConnectionHost,
                this.stream,
                this.clientEndPoint,
                this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("AUTH TLS"u8.ToArray());

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("234 "u8));

            await using SslDuplexPipe sslWrapper = await SslAuthenticator.AuthenticateAsClientAsync(
                this.writePipeReader, this.readPipeWriter, this.testCertificate.ValidationCallback);
            this.writePipeReader = sslWrapper.Input;
            this.readPipeWriter = sslWrapper.Output;

            await this.WriteLineAsync("PBSZ 0"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            await this.WriteLineAsync("PROT P"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            Mock<IFileProvider> fileProvider = new();
            this.mockControlConnectionHost.FileManager.FileProviders["anonymous"] = fileProvider.Object;

            await this.WriteLineAsync("USER anonymous"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("331 "u8));

            await this.WriteLineAsync("PASS"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("230 "u8));

            (CombinedStream stream, PipeWriter writer, PipeReader reader) = NetworkStreamMock.Create();
            fileProvider.Setup(x => x.CreateFileForWriteAsync("图片.jpg")).Returns(Task.FromResult(stream as Stream));
            {
                async Task RecieveAsync()
                {
                    int i = 0;
                    var result = await reader.ReadAsync(this.testContext.CancellationToken);
                    if (i == 100)
                    {
                        Assert.IsTrue(result.Buffer.IsEmpty);
                    }
                    else
                    {
                        SequenceReader<byte> sequenceReader = new(result.Buffer);
                        while (i < 100 && sequenceReader.TryRead(out byte b))
                        {
                            Assert.AreEqual((byte)i, b);
                            ++i;
                        }

                        reader.AdvanceTo(sequenceReader.Position);
                    }
                }

                Task receiveTask = RecieveAsync();
                using TcpClient dataClient = await this.ConnectWithEpsvAsync();

                await this.WriteLineAsync("STOR 图片.jpg"u8.ToArray());
                byte[]? storResponse = await this.ReadLineAsync();
                Assert.IsTrue(storResponse.AsSpan().StartsWith("125 "u8) || storResponse.AsSpan().StartsWith("150 "u8));

                using SslStream sslDataStream = new(dataClient.GetStream(), false, this.testCertificate.ValidationCallback);
                await sslDataStream.AuthenticateAsClientAsync(string.Empty);
                byte[] bytesToUpload = new byte[100];
                for (int i = 0; i < bytesToUpload.Length; ++i)
                {
                    bytesToUpload[i] = (byte)i;
                }

                await sslDataStream.WriteAsync(bytesToUpload, this.testContext.CancellationToken);
                await sslDataStream.ShutdownAsync();

                Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("226 "u8));
                await receiveTask;
            }

            await this.WriteLineAsync("QUIT"u8.ToArray());
            this.readPipeWriter.Complete();

            Assert.IsNull(await this.ReadLineAsync());
            await runTask;
        }

        /// <summary>
        /// Tests that PROT C command sets clear data connection.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task ProtCSetsClearDataConnectionTestAsync()
        {
            this.mockControlConnectionHost.ControlConnectionSslFactory = new ControlConnectionSslFactory(this.testCertificate.Certificate);
            using ControlConnection controlConnection = new(this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("PBSZ 0"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            await this.WriteLineAsync("PROT C"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            await this.WriteLineAsync("QUIT"u8.ToArray());
            this.readPipeWriter.Complete();

            Assert.IsNull(await this.ReadLineAsync());
            await runTask;
        }

        /// <summary>
        /// Tests that unsupported protection level in PROT command fails and returns 536 as in RFC 2228 section 3.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task UnsupportedProtectionLevelReturns536TestAsync()
        {
            this.mockControlConnectionHost.ControlConnectionSslFactory = new ControlConnectionSslFactory(this.testCertificate.Certificate);
            this.mockControlConnectionHost.DataConnector = new SslLocalDataConnectionFactory(this.testCertificate.Certificate);
            using ControlConnection controlConnection = new(this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("PBSZ 0"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            // Protection level S is not supported by TLS
            await this.WriteLineAsync("PROT S"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("536 "u8));

            // Protection level E is not supported by TLS
            await this.WriteLineAsync("PROT E"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("536 "u8));

            await this.WriteLineAsync("QUIT"u8.ToArray());
            this.readPipeWriter.Complete();

            Assert.IsNull(await this.ReadLineAsync());
            await runTask;
        }

        /// <summary>
        /// Tests that PROT P command without SSL data connection returns 504.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task ProtPWithoutSslDataConnectionReturns504TestAsync()
        {
            this.mockControlConnectionHost.ControlConnectionSslFactory = new ControlConnectionSslFactory(this.testCertificate.Certificate);
            using ControlConnection controlConnection = new(this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("PBSZ 0"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            await this.WriteLineAsync("PROT P"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("504 "u8));

            await this.WriteLineAsync("QUIT"u8.ToArray());
            this.readPipeWriter.Complete();

            Assert.IsNull(await this.ReadLineAsync());
            await runTask;
        }

        /// <summary>
        /// Tests that PROT with invalid parameter returns 504.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        [TestMethod]
        public async Task ProtInvalidParameterReturns504TestAsync()
        {
            this.mockControlConnectionHost.ControlConnectionSslFactory = new ControlConnectionSslFactory(this.testCertificate.Certificate);
            using ControlConnection controlConnection = new(this.mockControlConnectionHost, this.stream, this.clientEndPoint, this.serverEndPoint);
            Task runTask = controlConnection.RunAsync(this.testContext.CancellationToken);

            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("220 "u8));

            await this.WriteLineAsync("PBSZ 0"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            await this.WriteLineAsync("PROT X"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("504 "u8));

            await this.WriteLineAsync("QUIT"u8.ToArray());
            this.readPipeWriter.Complete();

            Assert.IsNull(await this.ReadLineAsync());
            await runTask;
        }

        private static int GetPortFromEpsvAddress(ReadOnlySpan<byte> epsvAddress)
        {
            byte epsvAddressDelimiter = epsvAddress[0];
            for (int i = 0; i < 3; ++i)
            {
                int index = epsvAddress.IndexOf(epsvAddressDelimiter);
                Assert.IsGreaterThanOrEqualTo(0, index);
                epsvAddress = epsvAddress[(index + 1)..];
            }

            int delimiterIndexAfterPort = epsvAddress.IndexOf(epsvAddressDelimiter);
            Assert.IsGreaterThanOrEqualTo(0, delimiterIndexAfterPort);
            Assert.AreEqual(epsvAddress.Length - 1, delimiterIndexAfterPort);
            ReadOnlySpan<byte> epsvPort = epsvAddress[..delimiterIndexAfterPort];
            var epsvPortNum = int.Parse(epsvPort, CultureInfo.InvariantCulture);
            return epsvPortNum;
        }

        private static ReadOnlySpan<byte> GetAddressFromEpsv(ReadOnlySpan<byte> epsvResponse)
        {
            // RFC 2428
            Assert.IsTrue(epsvResponse[..4].SequenceEqual("229 "u8));
            Assert.AreEqual((byte)')', epsvResponse[^1]);
            int startOfAddress = epsvResponse.LastIndexOf((byte)'(');
            Assert.IsGreaterThanOrEqualTo(0, startOfAddress);
            return epsvResponse[(startOfAddress + 1)..^1];
        }

        /// <summary>
        /// Helper method to read a single line from PipeReader.
        /// </summary>
        /// <param name="pipeReader">The PipeReader to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The line data read, or null if the end of the stream is reached.</returns>
        private static async Task<byte[]?> ReadLineFromReaderAsync(PipeReader pipeReader, CancellationToken cancellationToken)
        {
            while (true)
            {
                ReadResult readResult = await pipeReader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> readBuffer = readResult.Buffer;
                SequenceReader<byte> sequence = new(readBuffer);
                ReadOnlySpan<byte> delimiter = "\r\n"u8;
                if (sequence.TryReadTo(out ReadOnlySequence<byte> resultSequence, delimiter))
                {
                    byte[] result = resultSequence.ToArray();
                    pipeReader.AdvanceTo(readBuffer.GetPosition(delimiter.Length, resultSequence.End));
                    return result;
                }
                else if (readResult.IsCompleted)
                {
                    if (readBuffer.Length > 0)
                    {
                        byte[] result = readBuffer.ToArray();
                        pipeReader.AdvanceTo(readBuffer.GetPosition(readBuffer.Length));
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    pipeReader.AdvanceTo(sequence.Position, readBuffer.End);
                }
            }
        }

        private static (Dictionary<string, string> Facts, string PathName) ParseMlsXEntry(ReadOnlyMemory<byte> line)
        {
            // RFC 3659
            int factsEnd = line.Span.IndexOf((byte)' ');
            string pathName = Encoding.UTF8.GetString(line[(factsEnd + 1)..].Span);
            line = line[..factsEnd];

            Dictionary<string, string> facts = [];
            while (line.Span.IndexOf((byte)'=') is int factNameEnd && factNameEnd >= 0)
            {
                ReadOnlyMemory<byte> factName = line[..factNameEnd];
                line = line[(factNameEnd + 1)..];
                if (!(line.Span.IndexOf((byte)';') is int factValueEnd && factValueEnd >= 0))
                {
                    throw new ArgumentException($"Invalid fact. Expect \";\" in line \"{Encoding.UTF8.GetString(line.Span)}\"");
                }

                ReadOnlyMemory<byte> factValue = line[..factValueEnd];
                line = line[(factValueEnd + 1)..];
                facts.Add(Encoding.UTF8.GetString(factName.Span), Encoding.UTF8.GetString(factValue.Span));
            }

            Assert.AreEqual(0, line.Length);

            return (Facts: facts, PathName: pathName);
        }

        private static Dictionary<string, bool> ParseMLstFeatParams(ReadOnlyMemory<byte>? mlstFeatParam)
        {
            Assert.IsNotNull(mlstFeatParam);
            ReadOnlySpan<byte> mlstFeatParamSpan = mlstFeatParam.Value.Span;
            Dictionary<string, bool> facts = [];
            while (mlstFeatParamSpan.IndexOf((byte)';') is int index && index >= 0)
            {
                if (index > 1 && mlstFeatParamSpan[index - 1] == '*')
                {
                    facts.Add(Encoding.ASCII.GetString(mlstFeatParamSpan[..(index - 1)]), true);
                }
                else
                {
                    facts.Add(Encoding.ASCII.GetString(mlstFeatParamSpan[..index]), false);
                }

                mlstFeatParamSpan = mlstFeatParamSpan[(index + 1)..];
            }

            Assert.IsTrue(mlstFeatParamSpan.IsEmpty);
            return facts;
        }

        private async Task<byte[]?> ReadLineAsync()
        {
            if (this.writePipeReader is null)
            {
                throw new InvalidOperationException("The write pipe reader is not initialized.");
            }

            return await ReadLineFromReaderAsync(this.writePipeReader, this.testContext.CancellationToken);
        }

        private async Task<List<byte[]>> ReadLinesAsync(Stream stream)
        {
            Pipe pipe = new();
            List<byte[]> lines = [];

            async Task ReadAsync()
            {
                while (true)
                {
                    Memory<byte> buffer = pipe.Writer.GetMemory();
                    int length = await stream.ReadAsync(buffer, this.testContext.CancellationToken);
                    if (length == 0)
                    {
                        pipe.Writer.Complete();
                        return;
                    }

                    pipe.Writer.Advance(length);
                    await pipe.Writer.FlushAsync(this.testContext.CancellationToken);
                }
            }

            async Task ProcessAsync()
            {
                while (true)
                {
                    ReadResult readResult = await pipe.Reader.ReadAsync(this.testContext.CancellationToken);
                    ReadOnlySequence<byte> readBuffer = readResult.Buffer;
                    SequenceReader<byte> sequence = new(readBuffer);
                    ReadOnlySpan<byte> delimiter = "\r\n"u8;
                    if (sequence.TryReadTo(out ReadOnlySequence<byte> resultSequence, delimiter))
                    {
                        lines.Add(resultSequence.ToArray());
                        pipe.Reader.AdvanceTo(readBuffer.GetPosition(delimiter.Length, resultSequence.End));
                    }
                    else if (readResult.IsCompleted)
                    {
                        lines.Add(readBuffer.ToArray());
                        return;
                    }
                    else
                    {
                        pipe.Reader.AdvanceTo(sequence.Position, readBuffer.End);
                    }
                }
            }

            await Task.WhenAll(ReadAsync(), ProcessAsync());
            return lines;
        }

        private async Task WriteLineAsync(ReadOnlyMemory<byte> line)
        {
            await this.readPipeWriter.WriteAsync(line, this.testContext.CancellationToken);
            byte[] newLine = "\r\n"u8.ToArray();
            await this.readPipeWriter.WriteAsync(newLine, this.testContext.CancellationToken);
            await this.readPipeWriter.FlushAsync(this.testContext.CancellationToken);
        }

        private async Task<List<(ReadOnlyMemory<byte> FeatName, ReadOnlyMemory<byte>? FeatParam)>> ReadFeaturesAsync()
        {
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("211-"u8));
            List<(ReadOnlyMemory<byte> FeatName, ReadOnlyMemory<byte>? FeatParam)> features = [];
            while (true)
            {
                // RFC 2389 3.2
                byte[]? line = await this.ReadLineAsync();
                Assert.IsNotNull(line);
                if (line.AsSpan().StartsWith("211 "u8))
                {
                    break;
                }
                else
                {
                    Assert.IsTrue(line.AsSpan().StartsWith(" "u8));
                    ReadOnlyMemory<byte> fullFeature = line.AsMemory()[1..];
                    int spaceIndex = fullFeature.Span.IndexOf((byte)' ');
                    if (spaceIndex < 0)
                    {
                        features.Add((fullFeature, null));
                    }
                    else
                    {
                        features.Add((fullFeature[..spaceIndex], fullFeature[(spaceIndex + 1)..]));
                    }
                }
            }

            return features;
        }

        private async Task<TcpClient> ConnectWithEpsvAsync()
        {
            // RFC 2428
            await this.WriteLineAsync("EPSV"u8.ToArray());
            byte[]? epsvResponse = await this.ReadLineAsync();
            Assert.IsNotNull(epsvResponse);

            ReadOnlySpan<byte> epsvAddress = GetAddressFromEpsv(epsvResponse);
            int epsvPortNum = GetPortFromEpsvAddress(epsvAddress);
            TcpClient dataClient = new(this.serverEndPoint.AddressFamily);
            try
            {
                await dataClient.ConnectAsync(this.serverEndPoint.Address, epsvPortNum, this.testContext.CancellationToken);
                return dataClient;
            }
            catch (Exception)
            {
                dataClient.Dispose();
                throw;
            }
        }

        private sealed class MockControlConnectionHost : IControlConnectionHost
        {
            public FtpTracer Tracer { get; } = new();

            public IDataConnectionFactory DataConnector { get; set; } = new LocalDataConnectionFactory();

            public IAuthenticator Authenticator { get; } = new AnonymousAuthenticator();

            public MockFileProviderFactory FileManager { get; } = new MockFileProviderFactory();

            IFileProviderFactory IControlConnectionHost.FileManager => this.FileManager;

            public IControlConnectionSslFactory? ControlConnectionSslFactory { get; set; }
        }

        private sealed class MockFileProviderFactory : IFileProviderFactory
        {
            public Dictionary<string, IFileProvider> FileProviders { get; } = [];

            public IFileProvider GetProvider(string user) => this.FileProviders[user];
        }
    }
}