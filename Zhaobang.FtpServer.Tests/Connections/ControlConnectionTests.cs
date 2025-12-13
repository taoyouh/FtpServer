// <copyright file="ControlConnectionTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Moq;
using Zhaobang.FtpServer.Authenticate;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.File;
using Zhaobang.FtpServer.Tests.Mocks;
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
        private readonly PipeWriter readPipeWriter;
        private readonly PipeReader writePipeReader;
        private readonly CombinedStream stream;

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

            Assert.Contains(f => f.FeatName.Span.SequenceEqual("UTF8"u8) && f.FeatParam == null, features);

            await this.WriteLineAsync("OPTS UTF8 ON"u8.ToArray());
            Assert.IsTrue((await this.ReadLineAsync()).AsSpan().StartsWith("200 "u8));

            // RFC 2428
            await this.WriteLineAsync("EPSV"u8.ToArray());
            byte[]? epsvResponse = await this.ReadLineAsync();
            Assert.IsNotNull(epsvResponse);

            ReadOnlySpan<byte> epsvAddress = GetAddressFromEpsv(epsvResponse);
            var epsvPortNum = GetPortFromEpsvAddress(epsvAddress);
            using TcpClient dataClient = new(this.serverEndPoint.AddressFamily);
            await dataClient.ConnectAsync(this.serverEndPoint.Address, epsvPortNum, this.testContext.CancellationToken);

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

            // RFC 2428
            await this.WriteLineAsync("EPSV"u8.ToArray());
            byte[]? epsvResponse = await this.ReadLineAsync();
            Assert.IsNotNull(epsvResponse);

            ReadOnlySpan<byte> epsvAddress = GetAddressFromEpsv(epsvResponse);
            int epsvPortNum = GetPortFromEpsvAddress(epsvAddress);
            using TcpClient dataClient = new(this.serverEndPoint.AddressFamily);
            await dataClient.ConnectAsync(this.serverEndPoint.Address, epsvPortNum, this.testContext.CancellationToken);

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

        private sealed class MockControlConnectionHost : IControlConnectionHost
        {
            public FtpTracer Tracer { get; } = new();

            public IDataConnectionFactory DataConnector { get; } = new LocalDataConnectionFactory();

            public IAuthenticator Authenticator { get; } = new AnonymousAuthenticator();

            public MockFileProviderFactory FileManager { get; } = new MockFileProviderFactory();

            IFileProviderFactory IControlConnectionHost.FileManager => this.FileManager;

            public IControlConnectionSslFactory? ControlConnectionSslFactory => null;
        }

        private sealed class MockFileProviderFactory : IFileProviderFactory
        {
            public Dictionary<string, IFileProvider> FileProviders { get; } = [];

            public IFileProvider GetProvider(string user) => this.FileProviders[user];
        }
    }
}