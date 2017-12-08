using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Zhaobang.FtpServer.Connections
{
    public class SslLocalDataConnectionFactory : IDataConnectionFactory
    {
        public IDataConnection GetDataConnection(IPAddress localIP)
        {
            return new SslLocalDataConnection(localIP);
        }
    }
}
