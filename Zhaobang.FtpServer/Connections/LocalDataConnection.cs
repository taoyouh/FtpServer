// <copyright file="LocalDataConnection.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// Establish data connection from local sever.
    /// </summary>
    public class LocalDataConnection : IDisposable, IDataConnection
    {
        private readonly IPAddress listeningIP;

        [Obsolete("Use property instead.")]
        private ITcpConnection tcpConnection;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDataConnection"/> class.
        /// The class is used to maintain FTP data connection for ONE user.
        /// NO connection will be initiated immediately.
        /// </summary>
        /// <param name="localIP">The IP which was connected by the user.</param>
        public LocalDataConnection(IPAddress localIP)
        {
            listeningIP = localIP;
        }

        /// <summary>
        /// Gets a value indicating whether a data connection is open.
        /// </summary>
        public bool IsOpen
        {
            get
            {
                return this.TcpConnection?.Stream != null;
            }
        }

        /// <summary>
        /// Gets the supported protocal IDs in passive mode (defined in RFC 2824).
        /// </summary>
        public IEnumerable<int> SupportedPassiveProtocal
        {
            get
            {
                switch (listeningIP.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        return new[] { 1 };
                    case AddressFamily.InterNetworkV6:
                        return new[] { 2 };
                    default:
                        return new int[0];
                }
            }
        }

        /// <summary>
        /// Gets the supported protocal IDs in active mode (defined in RFC 2824).
        /// </summary>
        public IEnumerable<int> SupportedActiveProtocal
        {
            get => new int[] { 1, 2 };
        }

        private ITcpConnection TcpConnection
        {
#pragma warning disable CS0618
            get => this.tcpConnection;
            set
            {
                if (this.tcpConnection != value)
                {
                    if (this.tcpConnection != null)
                    {
                        this.tcpConnection.Dispose();
                    }

                    this.tcpConnection = value;
                }
            }
#pragma warning restore CS0618
        }

        /// <summary>
        /// Initiates a data connection in FTP active mode.
        /// </summary>
        /// <param name="remoteIP">The IP to connect to.</param>
        /// <param name="remotePort">The port to connect to.</param>
        /// <param name="protocal">Protocal ID defined in RFC 2428.</param>
        /// <returns>The task to await.</returns>
        public async Task ConnectActiveAsync(IPAddress remoteIP, int remotePort, int protocal)
        {
            AddressFamily addressFamily;
            switch (protocal)
            {
                case 1:
                    addressFamily = AddressFamily.InterNetwork;
                    break;
                case 2:
                    addressFamily = AddressFamily.InterNetworkV6;
                    break;
                default:
                    throw new NotSupportedException();
            }
            TcpConnection = null;
            var tcpClient = new TcpClient(addressFamily);
            await tcpClient.ConnectAsync(remoteIP, remotePort);
            this.TcpConnection = new ActiveTcpConnection(tcpClient);
        }

        /// <summary>
        /// Listens for FTP passive connection and returns the listening end point.
        /// </summary>
        /// <returns>The end point listening at.</returns>
        public IPEndPoint Listen()
        {
            try
            {
                // Open new connection before stopping pervious listener.
                var newConnection = new PassiveTcpConnection(listeningIP);
                this.TcpConnection = newConnection;
                return newConnection.ListenEndPoint;
            }
            catch (Exception)
            {
                this.TcpConnection = null;
                throw;
            }
        }

        /// <summary>
        /// Listens for FTP EPSV connection and returns the listening port.
        /// </summary>
        /// <param name="protocal">The protocal ID to use. Defined in RFC 2824.</param>
        /// <returns>The port listening at.</returns>
        public int ExtendedListen(int protocal)
        {
            if (SupportedPassiveProtocal.Contains(protocal))
                return Listen().Port;
            else
                throw new NotSupportedException();
        }

        /// <summary>
        /// Accepts a FTP passive mode connection.
        /// </summary>
        /// <returns>The task to await.</returns>
        /// <exception cref="InvalidOperationException">Not listening for incoming connection.</exception>
        /// <exception cref="OperationCanceledException">The current listener is closed before any incoming connection.</exception>
        public async Task AcceptAsync()
        {
            if (this.TcpConnection == null)
            {
                throw new InvalidOperationException("Not listening for incoming connection.");
            }

            await this.TcpConnection.WaitForClientAsync();
        }

        /// <summary>
        /// Disconnects any open connection.
        /// </summary>
        /// <returns>The task to await.</returns>
        public Task DisconnectAsync()
        {
            TcpConnection = null;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Copies content to data connection.
        /// </summary>
        /// <param name="streamToRead">The stream to copy from.</param>
        /// <returns>The task to await.</returns>
        public async Task SendAsync(Stream streamToRead)
        {
            NetworkStream stream = this.TcpConnection.Stream;
            await streamToRead.CopyToAsync(stream);
            await stream.FlushAsync();
        }

        /// <summary>
        /// Copies content from data connection.
        /// </summary>
        /// <param name="streamToWrite">The stream to copy to.</param>
        /// <returns>The task to await.</returns>
        public async Task RecieveAsync(Stream streamToWrite)
        {
            NetworkStream stream = this.TcpConnection.Stream;
            await stream.CopyToAsync(streamToWrite);
        }

        /// <summary>
        /// Close the connection and listener.
        /// </summary>
        public void Close()
        {
            TcpConnection = null;
        }

        /// <summary>
        /// Dispose of the connection and listener.
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}
