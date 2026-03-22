// <copyright file="SslDuplexPipe.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.IO.Pipelines;
using System.Net.Security;

namespace Zhaobang.FtpServer.Tests.Helpers
{
    /// <summary>
    /// Wraps an <see cref="SslStream"/> with pipe-based I/O.
    /// </summary>
    internal sealed class SslDuplexPipe : IDuplexPipe, IAsyncDisposable
    {
        private readonly SslStream sslStream;
        private readonly Pipe readPipe;
        private readonly Pipe writePipe;
        private readonly Task readTask;
        private readonly Task writeTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="SslDuplexPipe"/> class.
        /// </summary>
        /// <param name="sslStream">The SSL stream to wrap.</param>
        public SslDuplexPipe(SslStream sslStream)
        {
            this.sslStream = sslStream;
            this.readPipe = new();
            this.writePipe = new();

            this.readTask = this.ReadFromStreamAsync();
            this.writeTask = this.WriteToStreamAsync();
        }

        /// <inheritdoc/>
        public PipeReader Input => this.readPipe.Reader;

        /// <inheritdoc/>
        public PipeWriter Output => this.writePipe.Writer;

        /// <summary>
        /// Disposes the wrapper asynchronously and waits for background tasks to complete.
        /// </summary>
        /// <returns>A task representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            this.Input.Complete();
            this.Output.Complete();
            await Task.WhenAll(this.readTask, this.writeTask);
            await this.sslStream.DisposeAsync();
        }

        /// <summary>
        /// Copies data from the SSL stream to the read pipe.
        /// </summary>
        private async Task ReadFromStreamAsync()
        {
            try
            {
                await this.sslStream.CopyToAsync(this.readPipe.Writer.AsStream());
            }
            catch (Exception ex)
            {
                this.readPipe.Writer.Complete(ex);
                return;
            }
            finally
            {
                this.readPipe.Writer.Complete();
            }
        }

        /// <summary>
        /// Copies data from the write pipe to the SSL stream.
        /// </summary>
        private async Task WriteToStreamAsync()
        {
            try
            {
                await this.writePipe.Reader.CopyToAsync(this.sslStream);
                await this.sslStream.ShutdownAsync();
            }
            catch (Exception ex)
            {
                this.writePipe.Reader.Complete(ex);
                return;
            }
            finally
            {
                this.writePipe.Reader.Complete();
            }
        }
    }
}
