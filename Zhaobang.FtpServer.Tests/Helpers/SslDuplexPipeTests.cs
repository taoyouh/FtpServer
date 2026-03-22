// <copyright file="SslDuplexPipeTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.IO.Pipelines;
using System.Net.Security;
using Moq;

namespace Zhaobang.FtpServer.Tests.Helpers
{
    /// <summary>
    /// Unit tests for class <see cref="SslDuplexPipe"/>.
    /// </summary>
    [TestClass]
    public class SslDuplexPipeTests
    {
        private readonly TestContext testContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="SslDuplexPipeTests"/> class.
        /// </summary>
        /// <param name="testContext">The test context.</param>
        public SslDuplexPipeTests(TestContext testContext)
        {
            ArgumentNullException.ThrowIfNull(testContext);
            this.testContext = testContext;
        }

        /// <summary>
        /// Tests that an exception thrown by the SSL stream is propagated to the pipe writer.
        /// </summary>
        /// <returns>The task representing the asynchrounous operation.</returns>
        [TestMethod]
        public async Task SslWriteExceptionThrownOnPipeWriterTestAsync()
        {
            Mock<SslStream> mockSslStream = new(() => new SslStream(new MemoryStream()));
            mockSslStream
                .Setup(s => s.WriteAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Test exception"));
            await using SslDuplexPipe sslPipe = new(mockSslStream.Object);

            byte[] buffer = new byte[PipeOptions.Default.PauseWriterThreshold + 1];
            await Assert.ThrowsAsync<IOException>(async () =>
                await sslPipe.Output.WriteAsync(buffer, this.testContext.CancellationToken));
        }

        /// <summary>
        /// Tests that the SSL stream is shut down when the pipe writer is completed.
        /// </summary>
        /// <returns>The task representing the asynchrounous operation.</returns>
        [TestMethod]
        public async Task PipeWriteCompletionShutsDownSslStreamTestAsync()
        {
            Mock<SslStream> mockSslStream = new(() => new SslStream(new MemoryStream()));
            TaskCompletionSource shutdownTaskSource = new();
            mockSslStream
                .Setup(s => s.ShutdownAsync())
                .Returns(() =>
                {
                    shutdownTaskSource.SetResult();
                    return Task.CompletedTask;
                });
            await using (SslDuplexPipe sslPipe = new(mockSslStream.Object))
            {
                sslPipe.Output.Complete();
                await shutdownTaskSource.Task;
                mockSslStream.Verify(s => s.ShutdownAsync(), Times.Once);
            }

            mockSslStream.Verify(s => s.ShutdownAsync(), Times.Once);
        }

        /// <summary>
        /// Tests that an exception thrown by the SSL stream is propagated to the pipe reader.
        /// </summary>
        /// <returns>The task representing the asynchrounous operation.</returns>
        [TestMethod]
        public async Task SslReadExceptionThrownOnPipeReaderTestAsync()
        {
            Mock<SslStream> mockSslStream = new(() => new SslStream(new MemoryStream()), MockBehavior.Default);
            mockSslStream
                .Setup(s => s.CopyToAsync(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Test exception"));
            await using SslDuplexPipe sslPipe = new(mockSslStream.Object);
            await Assert.ThrowsAsync<IOException>(async () => await sslPipe.Input.ReadAsync(this.testContext.CancellationToken));
        }

        /// <summary>
        /// Tests that the pipe reader reads to completion when the SSL stream reads to completion.
        /// </summary>
        /// <returns>The task representing the asynchrounous operation.</returns>
        [TestMethod]
        public async Task SslReadCompletionCompletesPipeReaderTestAsync()
        {
            Mock<SslStream> mockSslStream = new(() => new SslStream(new MemoryStream()));
            TaskCompletionSource copyToTaskSource = new();
            mockSslStream
                .Setup(s => s.CopyToAsync(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(copyToTaskSource.Task);
            await using SslDuplexPipe sslPipe = new(mockSslStream.Object);
            copyToTaskSource.SetResult();
            Assert.IsTrue((await sslPipe.Input.ReadAsync(this.testContext.CancellationToken)).IsCompleted);
        }
    }
}