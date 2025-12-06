// <copyright file="IControlConnectionHost.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using Zhaobang.FtpServer.Authenticate;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.File;
using Zhaobang.FtpServer.Trace;

namespace Zhaobang.FtpServer
{
    /// <summary>
    /// The host of <see cref="ControlConnection"/>.
    /// </summary>
    internal interface IControlConnectionHost
    {
        /// <summary>
        /// Gets the instance of <see cref="FtpTracer"/> to trace FTP commands and replies.
        /// </summary>
        FtpTracer Tracer { get; }

        /// <summary>
        /// Gets the manager that provides <see cref="IDataConnectionFactory"/> for each user.
        /// </summary>
        IDataConnectionFactory DataConnector { get; }

        /// <summary>
        /// Gets the manager that authenticates user.
        /// </summary>
        IAuthenticator Authenticator { get; }

        /// <summary>
        /// Gets the manager that provides <see cref="IFileProviderFactory"/> for each user.
        /// </summary>
        IFileProviderFactory FileManager { get; }

        /// <summary>
        /// Gets the factory to upgrade control connection to an encrypted one. May be null.
        /// </summary>
        IControlConnectionSslFactory ControlConnectionSslFactory { get; }
    }
}