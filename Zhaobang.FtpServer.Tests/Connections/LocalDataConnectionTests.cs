// <copyright file="LocalDataConnectionTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Zhaobang.FtpServer.Connections;

namespace Zhaobang.FtpServer.Tests.Connections
{
    /// <summary>
    /// Tests for <see cref="LocalDataConnection"/>.
    /// </summary>
    [TestClass]
    public class LocalDataConnectionTests : DataConnectionTests<LocalDataConnection>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDataConnectionTests"/> class.
        /// </summary>
        /// <param name="testContext">The test context.</param>
        public LocalDataConnectionTests(TestContext testContext)
            : base(testContext)
        {
        }

        /// <inheritdoc/>
        protected override LocalDataConnection CreateDataConnection(IPAddress serverIp)
        {
            return new LocalDataConnection(serverIp);
        }
    }
}
