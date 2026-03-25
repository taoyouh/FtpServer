// <copyright file="SslAuthenticator.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.IO.Pipelines;
using System.Net.Security;

namespace Zhaobang.FtpServer.Tests.Helpers
{
    /// <summary>
    /// Provides SSL/TLS authentication functionality for mock network streams.
    /// </summary>
    internal static class SslAuthenticator
    {
        /// <summary>
        /// Authenticates an SSL/TLS connection as a client and returns a wrapped stream with pipe-based I/O.
        /// </summary>
        /// <param name="pipeReader">The reader for receiving underlying raw encrypted data.</param>
        /// <param name="pipeWriter">The writer for sending underlying raw encrypted data.</param>
        /// <param name="validationCallback">The callback used for validating the server certificate. If null, default validation is used.</param>
        /// <param name="targetHost">The target host name for SSL authentication. Defaults to an empty string.</param>
        /// <returns>A <see cref="SslDuplexPipe"/> that manages the SSL stream and background I/O tasks.</returns>
        public static async Task<SslDuplexPipe> AuthenticateAsClientAsync(
            PipeReader pipeReader,
            PipeWriter pipeWriter,
            RemoteCertificateValidationCallback? validationCallback = null,
            string targetHost = "")
        {
            CombinedStream rawDataStream = new(pipeReader.AsStream(), pipeWriter.AsStream());
            SslStream sslStream = new(rawDataStream, false, validationCallback);
            await sslStream.AuthenticateAsClientAsync(targetHost);
            return new SslDuplexPipe(sslStream);
        }
    }
}
