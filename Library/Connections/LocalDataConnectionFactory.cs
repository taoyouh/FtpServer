// <copyright file="LocalDataConnectionFactory.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// Manager to provide <see cref="LocalDataConnection"/> for each user.
    /// Data connections are established from local server.
    /// </summary>
    public class LocalDataConnectionFactory : IDataConnectionFactory
    {
        /// <summary>
        /// Gets <see cref="LocalDataConnection"/> for a user.
        /// </summary>
        /// <param name="localIP">The IP which was connected by the user.</param>
        /// <param name="minPort">The min port in PASSIVE mode.</param>
        /// <param name="maxPort">The max port in PASSIVE mode.</param>
        /// <returns>The data connection for the user.</returns>
        public IDataConnection GetDataConnection(IPAddress localIP, int minPort, int maxPort)
        {
            return new LocalDataConnection(localIP, minPort, maxPort);
        }
    }
}
