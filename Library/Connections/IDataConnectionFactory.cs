// <copyright file="IDataConnectionFactory.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Zhaobang.FtpServer.Connections
{
    /// <summary>
    /// The interface that provides <see cref="IDataConnection"/> for each control connection.
    /// </summary>
    public interface IDataConnectionFactory
    {
        /// <summary>
        /// Gets <see cref="IDataConnection"/> for a control connection.
        /// </summary>
        /// <param name="localIP">The server IP that was connected by the user.</param>
        /// <param name="minPort">The min port in PASSIVE mode.</param>
        /// <param name="maxPort">The max port in PASSIVE mode.</param>
        /// <returns>The <see cref="IDataConnection"/> for that control connection.</returns>
        IDataConnection GetDataConnection(IPAddress localIP, int minPort, int maxPort);
    }
}
