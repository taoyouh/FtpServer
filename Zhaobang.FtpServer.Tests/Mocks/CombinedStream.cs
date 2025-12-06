// <copyright file="CombinedStream.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

namespace Zhaobang.FtpServer.Tests.Mocks
{
    /// <summary>
    /// A stream to combine a stream for reading and a stream for writing.
    /// </summary>
    internal sealed class CombinedStream : Stream
    {
        private readonly Stream readStream;
        private readonly Stream writeStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="CombinedStream"/> class.
        /// </summary>
        /// <param name="readStream">The stream used for reading.</param>
        /// <param name="writeStream">The stream used for writing.</param>
        public CombinedStream(Stream readStream, Stream writeStream)
        {
            ArgumentNullException.ThrowIfNull(readStream);
            ArgumentNullException.ThrowIfNull(writeStream);
            this.readStream = readStream;
            this.writeStream = writeStream;
        }

        /// <summary>
        /// Gets the stream used for reading.
        /// </summary>
        public Stream ReadStream => this.readStream;

        /// <summary>
        /// Gets the stream used for writing.
        /// </summary>
        public Stream WriteStream => this.writeStream;

        /// <summary>
        /// Gets a value indicating whether the stream can be read from. Returns <see cref="Stream.CanRead"/> of <see cref="ReadStream"/>.
        /// </summary>
        public override bool CanRead => this.readStream.CanRead;

        /// <summary>
        /// Gets a value indicating whether the stream can seek. Always returns false because this stream does not support seeking.
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Gets a value indicating whether the stream can be written to. Returns <see cref="Stream.CanWrite"/> of <see cref="WriteStream"/>.
        /// </summary>
        public override bool CanWrite => this.writeStream.CanWrite;

        /// <summary>
        /// Gets the length of the stream. Throws <see cref="NotSupportedException"/> because this stream does not support seeking.
        /// </summary>
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// Gets or sets the position of the stream. Throws <see cref="NotSupportedException"/> because this stream does not support seeking.
        /// </summary>
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <summary>
        /// Flushes the buffer in <see cref="WriteStream"/> and write the data to the underlying device.
        /// </summary>
        public override void Flush()
        {
            this.writeStream.Flush();
        }

        /// <summary>
        /// Reads from <see cref="ReadStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.readStream.Read(buffer, offset, count);
        }

        /// <summary>
        /// Reads from <see cref="ReadStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            return this.readStream.Read(buffer);
        }

        /// <summary>
        /// Reads from <see cref="ReadStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.readStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// Reads from <see cref="ReadStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return this.readStream.ReadAsync(buffer, cancellationToken);
        }

        /// <summary>
        /// Reads from <see cref="ReadStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override int ReadByte()
        {
            return this.readStream.ReadByte();
        }

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> because this stream does not support seeking.
        /// </summary>
        /// <param name="offset">The offset to seek to.</param>
        /// <param name="origin">The origin from which to seek.</param>
        /// <returns>The new position after seek.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> because this stream does not have a length.
        /// </summary>
        /// <param name="value">The new length.</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes to <see cref="WriteStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.writeStream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Writes to <see cref="WriteStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            this.writeStream.Write(buffer);
        }

        /// <summary>
        /// Writes to <see cref="WriteStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.writeStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// Writes to <see cref="WriteStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return this.writeStream.WriteAsync(buffer, cancellationToken);
        }

        /// <summary>
        /// Writes to <see cref="WriteStream"/>.
        /// </summary>
        /// <inheritdoc/>
        public override void WriteByte(byte value)
        {
            this.writeStream.WriteByte(value);
        }

        /// <summary>
        /// Asynchronously disposes <see cref="ReadStream"/> and <see cref="WriteStream"/>.
        /// </summary>
        /// <returns>The task representing the asynchrounous disposal operation.</returns>
        public override async ValueTask DisposeAsync()
        {
            ValueTask readDisposeTask = this.readStream.DisposeAsync();
            ValueTask writeDisposeTask = this.writeStream.DisposeAsync();
            await readDisposeTask;
            await writeDisposeTask;
            await base.DisposeAsync();
        }

        /// <summary>
        /// Disposes <see cref="ReadStream"/> and <see cref="WriteStream"/> if called from Dispose.
        /// Does nothing if called from finalizer.
        /// </summary>
        /// <param name="disposing">true if called from Dispose.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.readStream.Dispose();
                this.writeStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
