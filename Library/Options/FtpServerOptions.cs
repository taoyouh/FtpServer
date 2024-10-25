// <copyright file="FtpServerOptions.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Zhaobang.FtpServer.Options
{
    /// <summary>
    /// Ftp server options.
    /// </summary>
    public class FtpServerOptions
    {
        /// <summary>
        /// Gets or sets the min port of the FTP file system in passive mode.
        /// </summary>
        public string PassiveIp { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the min port of the FTP file system in passive mode.
        /// </summary>
        public int PassiveMinPort { get; set; } = 1024;

        /// <summary>
        /// Gets or sets the max port of the FTP file system in passive mode.
        /// </summary>
        public int PassiveMaxPort { get; set; } = 65535;
    }
}
