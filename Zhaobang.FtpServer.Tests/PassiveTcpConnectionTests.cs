// <copyright file="PassiveTcpConnectionTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.Net;
using System.Net.Sockets;
using Zhaobang.FtpServer.Connections;

namespace Zhaobang.FtpServer.Tests
{
    /// <summary>
    /// Tests for <see cref="PassiveTcpConnection"/>.
    /// </summary>
    [TestClass]
    public sealed class PassiveTcpConnectionTests
    {
        /// <summary>
        /// When IPv4 address is provided to PassiveTcpConnection constructor,
        /// the ListenEndPoint should also be IPv4.
        /// </summary>
        [TestMethod]
        public void IPv4AddressShouldProduceIPv4ListenEndPoint()
        {
            // Arrange
            IPAddress ipv4Address = IPAddress.Loopback;

            // Act
            using var connection = new PassiveTcpConnection(ipv4Address);
            var listenEndPoint = connection.ListenEndPoint;

            // Assert
            Assert.AreEqual(
                AddressFamily.InterNetwork,
                listenEndPoint.AddressFamily,
                "ListenEndPoint should be IPv4 (InterNetwork)");
            Assert.AreEqual(
                AddressFamily.InterNetwork,
                listenEndPoint.Address.AddressFamily,
                "ListenEndPoint.Address should be IPv4 (InterNetwork)");
        }

        /// <summary>
        /// When IPv6 address is provided to PassiveTcpConnection constructor,
        /// the ListenEndPoint should also be IPv6.
        /// </summary>
        [TestMethod]
        public void IPv6AddressShouldProduceIPv6ListenEndPoint()
        {
            // Arrange
            IPAddress ipv6Address = IPAddress.IPv6Loopback;

            // Act
            using var connection = new PassiveTcpConnection(ipv6Address);
            var listenEndPoint = connection.ListenEndPoint;

            // Assert
            Assert.AreEqual(
                AddressFamily.InterNetworkV6,
                listenEndPoint.AddressFamily,
                "ListenEndPoint should be IPv6 (InterNetworkV6)");
            Assert.AreEqual(
                AddressFamily.InterNetworkV6,
                listenEndPoint.Address.AddressFamily,
                "ListenEndPoint.Address should be IPv6 (InterNetworkV6)");
        }
    }
}
