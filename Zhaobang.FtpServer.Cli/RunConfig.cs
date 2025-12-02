// <copyright file="RunConfig.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Text;
using System.Xml.Serialization;

namespace Zhaobang.FtpServer
{
    /// <summary>
    /// The configuration of a server run.
    /// </summary>
    public class RunConfig
    {
        /// <summary>
        /// Gets the default configuration.
        /// </summary>
        public static RunConfig Default
        {
            get
            {
                var config = new RunConfig();
                config.EndPoints.Add(new IPEndPointData
                {
                    Address = IPAddress.Any,
                    Port = 21,
                });
                config.EndPoints.Add(new IPEndPointData
                {
                    Address = IPAddress.IPv6Any,
                    Port = 21,
                });
                return config;
            }
        }

        /// <summary>
        /// Gets or sets the local end points that the server should listen to.
        /// </summary>
        public List<IPEndPointData> EndPoints { get; set; } = new List<IPEndPointData>();

        /// <summary>
        /// Gets or sets the root directory of the FTP file system.
        /// </summary>
        public string BaseDirectory { get; set; } = "./";

        /// <summary>
        /// Gets or sets the path to the PKCS7 certificate file if TLS is enabled, or empty string if TLS is not enabled.
        /// </summary>
        public string CertificatePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password of the PKCS7 certificate file if TLS is enabled.
        /// </summary>
        public string CertificatePassword { get; set; } = string.Empty;

        /// <summary>
        /// An end point to listen on.
        /// </summary>
        public struct IPEndPointData
        {
            /// <summary>
            /// Gets or sets the address to listen on.
            /// </summary>
            [XmlIgnore]
            public IPAddress Address { get; set; }

            /// <summary>
            /// Gets or sets the string representing <see cref="Address"/>.
            /// </summary>
            [XmlAttribute(AttributeName = "Address")]
            public string AddressString
            {
                get => Address.ToString();
                set => Address = IPAddress.Parse(value);
            }

            /// <summary>
            /// Gets or sets the port to listen on.
            /// </summary>
            [XmlAttribute]
            public int Port { get; set; }

            public static implicit operator IPEndPoint(IPEndPointData ep)
            {
                return new IPEndPoint(ep.Address, ep.Port);
            }

            /// <summary>
            /// Convert the IP end point to a string.
            /// </summary>
            /// <returns>The string form of the instance.</returns>
            public override string ToString()
            {
                return ((IPEndPoint)this).ToString();
            }
        }
    }
}
