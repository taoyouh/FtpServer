// <copyright file="DataConnector.cs" company="Zhaoquan Huang">
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
    /// Manager to provide <see cref="DataConnection"/> for each user
    /// </summary>
    public class DataConnector
    {
        /// <summary>
        /// Gets <see cref="DataConnection"/> for a user
        /// </summary>
        /// <param name="localIP">The IP which was connected by the user</param>
        /// <returns>The data connection for the user</returns>
        public virtual DataConnection GetDataConnection(IPAddress localIP)
        {
            return new DataConnection(localIP);
        }
    }
}
