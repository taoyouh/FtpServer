// <copyright file="SslLocalDataConnection.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

#if NETSTANDARD2_1
namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// Establish data connection from local sever.
    /// </summary>
    public class SslLocalDataConnection : LocalDataConnection, ISslDataConnection
    {
        private readonly X509Certificate certificate;
        private WeakReference<Stream> tcpStream;

        [Obsolete("Use property instead.")]
        private SslStream encryptedStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="SslLocalDataConnection"/> class.
        /// The class is used to maintain FTP data connection for ONE user.
        /// NO connection will be initiated immediately.
        /// </summary>
        /// <param name="localIP">The IP which was connected by the user.</param>
        /// <param name="certificate">The certificate to upgrade to encrypted stream.</param>
        public SslLocalDataConnection(IPAddress localIP, X509Certificate certificate)
            : base(localIP)
        {
            this.certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        }

        private SslStream EncryptedStream
        {
            #pragma warning disable CS0618 // Type or member is obsolete
            get => this.encryptedStream;
            set
            {
                if (this.encryptedStream != value)
                {
                    this.encryptedStream?.Dispose();
                    this.encryptedStream = value;
                }
            }
            #pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            this.EncryptedStream = null;
            base.Dispose(disposing);
        }

        /// <summary>
        /// Upgrade the connection to SSL stream.
        /// </summary>
        /// <returns>The task of the async operation.</returns>
        public async Task UpgradeToSslAsync()
        {
            this.ThrowIfDisposed();
            this.SyncStreams();
            if (this.EncryptedStream != null)
            {
                throw new InvalidOperationException("Already upgraded to SSL stream.");
            }

            if (this.tcpStream == null || !this.tcpStream.TryGetTarget(out Stream tcpStream))
            {
                throw new InvalidOperationException("TCP stream is not available.");
            }

            var sslStream = new SslStream(tcpStream, false);
            await sslStream.AuthenticateAsServerAsync(certificate);
            this.EncryptedStream = sslStream;
        }

        /// <inheritdoc/>
        private protected override Stream GetStream()
        {
            this.SyncStreams();
            if (this.EncryptedStream != null)
            {
                return this.EncryptedStream;
            }

            if (this.tcpStream?.TryGetTarget(out Stream tcpStream) == true)
            {
                return tcpStream;
            }

            return null;
        }

        /// <inheritdoc/>
        private protected override async Task DisconnectCoreAsync()
        {
            this.SyncStreams();

            if (this.EncryptedStream != null)
            {
                await this.EncryptedStream.ShutdownAsync();
            }

            await base.DisconnectCoreAsync();
        }

        private void SyncStreams()
        {
            if (this.TcpConnection.Stream == null)
            {
                this.tcpStream = null;
                this.EncryptedStream = null;
            }
            else if (this.tcpStream == null
                || !this.tcpStream.TryGetTarget(out Stream tcpStream)
                || tcpStream != this.TcpConnection.Stream)
            {
                this.tcpStream = new WeakReference<Stream>(this.TcpConnection.Stream);
                this.EncryptedStream = null;
            }
        }
    }
}
#endif
