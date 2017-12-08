using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Zhaobang.FtpServer.Connections
{
    public interface ISslDataConnection
    {
        Task UpgradeToSslAsync(X509Certificate certificate);
    }
}
