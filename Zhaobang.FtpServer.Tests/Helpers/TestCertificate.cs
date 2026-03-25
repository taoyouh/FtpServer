// <copyright file="TestCertificate.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Zhaobang.FtpServer.Tests.Helpers
{
    /// <summary>
    /// Helper class to generate self-signed X509Certificate2 for testing SSL/TLS functionality.
    /// </summary>
    internal sealed class TestCertificate
    {
        private readonly Lazy<X509Certificate2> certificate = new(CreateSelfSignedCertificate);

        /// <summary>
        /// Gets the self-signed certificate for testing purposes.
        /// </summary>
        public X509Certificate2 Certificate => this.certificate.Value;

        /// <summary>
        /// Gets a certificate validation callback that validates this test certificate.
        /// </summary>
        /// <remarks>
        /// This callback validates that the remote certificate matches the test certificate
        /// by comparing the certificate thumbprints. This allows testing with self-signed
        /// certificates without disabling all certificate validation.
        /// </remarks>
        public RemoteCertificateValidationCallback ValidationCallback => this.ValidateCertificate;

        /// <summary>
        /// Creates a self-signed certificate for testing SSL/TLS connections.
        /// </summary>
        /// <returns>A self-signed X509Certificate2.</returns>
        private static X509Certificate2 CreateSelfSignedCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new(
                "CN=localhost",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            SubjectAlternativeNameBuilder sanBuilder = new();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName("127.0.0.1");
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            request.CertificateExtensions.Add(sanBuilder.Build());

            X509Certificate2 certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(1));

            byte[] pfxBytes = certificate.Export(X509ContentType.Pfx);
            return new X509Certificate2(pfxBytes, (string?)null, X509KeyStorageFlags.Exportable);
        }

        /// <summary>
        /// Validates that the remote certificate matches the expected test certificate.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="certificate">The certificate to validate.</param>
        /// <param name="chain">The X.509 chain.</param>
        /// <param name="sslPolicyErrors">The SSL policy errors.</param>
        /// <returns>True if the certificate matches the test certificate; otherwise, false.</returns>
        private bool ValidateCertificate(
            object? sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null)
            {
                return false;
            }

            if (!this.certificate.IsValueCreated)
            {
                return false;
            }

            return this.Certificate.GetCertHash().SequenceEqual(certificate.GetCertHash());
        }
    }
}
