// <copyright file="NetworkStreamMock.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.IO.Pipelines;

namespace Zhaobang.FtpServer.Tests.Mocks
{
    /// <summary>
    /// Utility to create a mock network stream.
    /// </summary>
    internal static class NetworkStreamMock
    {
        /// <summary>
        /// Create a mock stream, which reads from a reading pipe and writes to a writing pipe.
        /// </summary>
        /// <returns>The mock stream (Stream), the writer for the stream's reads (ReadPipeWriter), and the reader for the stream's writes (WritePipeReader).</returns>
        public static (CombinedStream Stream, PipeWriter ReadPipeWriter, PipeReader WritePipeReader) Create()
        {
            Pipe readPipe = new();
            Pipe writePipe = new();
            CombinedStream stream = new(readPipe.Reader.AsStream(), writePipe.Writer.AsStream());
            return new(stream, readPipe.Writer, writePipe.Reader);
        }
    }
}
