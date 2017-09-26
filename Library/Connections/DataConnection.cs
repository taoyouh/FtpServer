// <copyright file="DataConnection.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// The maintainer of FTP data connection for ONE user
    /// </summary>
    public class DataConnection : IDisposable
    {
        private static LinkedList<int> availablePorts = new LinkedList<int>();

        private readonly IPAddress listeningIP;

        private TcpClient tcpClient;

        /// <summary>
        /// The port number used in passive mode.
        /// If changed to active mode, set to -1.
        /// </summary>
        private int listeningPort;
        private TcpListener tcpListener;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataConnection"/> class.
        /// The class is used to maintain FTP data connection for ONE user.
        /// NO connection will be initiated immediately.
        /// </summary>
        /// <param name="localIP">The IP which was connected by the user</param>
        public DataConnection(IPAddress localIP)
        {
            listeningIP = localIP;
        }

        /// <summary>
        /// Gets a value indicating whether a data connection is open
        /// </summary>
        public virtual bool IsOpen
        {
            get { return TcpClient != null && TcpClient.Connected; }
        }

        private TcpClient TcpClient
        {
            get => tcpClient;
            set
            {
                {
                    if (tcpClient != null)
                    {
                        tcpClient.Dispose();
                        if (listeningPort != -1)
                        {
                            lock (availablePorts)
                            {
                                availablePorts.AddLast(listeningPort);
                            }
                            listeningPort = -1;
                        }
                    }
                    if (tcpClient != value)
                        tcpClient = value;
                }
            }
        }

        /// <summary>
        /// Initiates a data connection in FTP active mode
        /// </summary>
        /// <param name="remoteIP">The IP to connect to</param>
        /// <param name="remotePort">The port to connect to</param>
        /// <returns>The task to await</returns>
        public virtual async Task ConnectActiveAsync(IPAddress remoteIP, int remotePort)
        {
            listeningPort = -1;
            TcpClient = new TcpClient();
            await TcpClient.ConnectAsync(remoteIP, remotePort);
        }

        /// <summary>
        /// Listens for FTP passive connection and returns the listening end point
        /// </summary>
        /// <returns>The end point listening at</returns>
        public virtual IPEndPoint Listen()
        {
            if (tcpListener != null)
            {
                try
                {
                    tcpListener.Start();
                    return new IPEndPoint(listeningIP, listeningPort);
                }
                catch { }
            }
            int port = 1050;
            lock (availablePorts)
            {
                if (availablePorts.First != null)
                {
                    port = availablePorts.First.Value;
                    availablePorts.RemoveFirst();
                }
            }
            while (port < 65536)
            {
                try
                {
                    listeningPort = port;
                    var listeningEP = new IPEndPoint(listeningIP, listeningPort);
                    tcpListener = new TcpListener(listeningEP);
                    tcpListener.Start();
                    return listeningEP;
                }
                catch
                {
                    port++;
                }
            }
            throw new Exception("There are no ports available");
        }

        /// <summary>
        /// Accepts a FTP passive mode connection
        /// </summary>
        /// <returns>The task to await</returns>
        public virtual async Task AcceptAsync()
        {
            tcpClient = await tcpListener.AcceptTcpClientAsync();
            tcpListener.Stop();
            tcpListener = null;
        }

        /// <summary>
        /// Disconnects any open connection
        /// </summary>
        /// <returns>The task to await</returns>
#pragma warning disable CS1998
        public virtual async Task DisconnectAsync()
#pragma warning restore CS1998
        {
            TcpClient = null;
        }

        /// <summary>
        /// Copies content to data connection
        /// </summary>
        /// <param name="streamToRead">The stream to copy from</param>
        /// <returns>The task to await</returns>
        public virtual async Task SendAsync(Stream streamToRead)
        {
            var stream = tcpClient.GetStream();
            await streamToRead.CopyToAsync(stream);
            await stream.FlushAsync();
        }

        /// <summary>
        /// Copies content from data connection
        /// </summary>
        /// <param name="streamToWrite">The stream to copy to</param>
        /// <returns>The task to await</returns>
        public virtual async Task RecieveAsync(Stream streamToWrite)
        {
            var stream = tcpClient.GetStream();
            await stream.CopyToAsync(streamToWrite);
        }

        /// <summary>
        /// Dispose of the connection and listener
        /// </summary>
        public void Dispose()
        {
            if (TcpClient != null)
            {
                ((IDisposable)TcpClient).Dispose();
            }
            tcpListener.Stop();
            if (listeningPort >= 0)
            {
                lock (availablePorts)
                {
                    availablePorts.AddLast(listeningPort);
                }
            }
        }
    }
}
