using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Xml.Serialization;

namespace Zhaobang.FtpServer
{
    public class RunConfig
    {
        public static RunConfig Default
        {
            get
            {
                var config = new RunConfig();
                config.EndPoints.Add(new IPEndPointData
                {
                    Address = IPAddress.Any,
                    Port = 21
                });
                config.EndPoints.Add(new IPEndPointData
                {
                    Address = IPAddress.IPv6Any,
                    Port = 21
                });
                return config;
            }
        }

        public List<IPEndPointData> EndPoints { get; set; } = new List<IPEndPointData>();

        public string BaseDirectory { get; set; } = "./";

        public struct IPEndPointData
        {
            [XmlIgnore]
            public IPAddress Address { get; set; }

            [XmlAttribute(AttributeName = "Address")]
            public string AddressString
            {
                get => Address.ToString();
                set => Address = IPAddress.Parse(value);
            }

            [XmlAttribute]
            public int Port { get; set; }

            public static implicit operator IPEndPoint(IPEndPointData ep)
            {
                return new IPEndPoint(ep.Address, ep.Port);
            }

            public override string ToString()
            {
                return ((IPEndPoint)this).ToString();
            }
        }
    }
}
