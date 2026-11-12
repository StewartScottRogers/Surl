using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// DNS over TLS's ALPN (ADR-0092 decision 5, RFC 7858, RFC 7301 section 3.2): a <see cref="StreamConnection"/>
/// given the handshake <see cref="ServerTlsHandshake.ForListener"/> makes for a <c>dns</c> listen URL that is
/// TLS from the first byte agrees <c>dot</c>, accepts a client offering no ALPN and ends a handshake whose
/// client offers only other protocols. The handshakes run over an in-memory duplex pipe, so no socket is opened.
/// </summary>
[TestClass]
public sealed class ServerTlsHandshakeDnsOverTlsTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 853);
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);
    private static readonly FixedTimeProvider Time = new(TestCertificates.Now);
    private static readonly ListenUrl DnsOverTls = new("dns", "127.0.0.1", 853) { IsImplicitTls = true };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ForListener_DnsOverTls_OffersExactlyDot()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);

        var handshake = ServerTlsHandshake.ForListener(settings, DnsOverTls);

        CollectionAssert.AreEqual(new[] { new SslApplicationProtocol("dot") }, handshake!.ApplicationProtocols.ToArray());
    }

    [TestMethod]
    [DataRow("dot", "dot")]
    [DataRow("h2,dot", "dot")]
    [DataRow("", "")]
    public async Task UpgradeToTlsAsync_DnsOverTlsClient_AgreesDotOrNoProtocol(string offered, string expected)
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(
            serverStream, Local, Remote, new DuplexTransportControl(serverStream), ServerTlsHandshake.ForListener(settings, DnsOverTls));
        await using var client = new SslStream(clientStream, leaveInnerStreamOpen: true);

        var upgrade = connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        await client.AuthenticateAsClientAsync(ClientOptions(offered), TestContext.CancellationToken);
        await upgrade;

        Assert.AreEqual(expected, System.Text.Encoding.ASCII.GetString(client.NegotiatedApplicationProtocol.Protocol.Span));
        Assert.IsNotNull(connection.TlsSession);
    }

    // With ALPN offered and none in common, OpenSSL ends the handshake with no_application_protocol (120)
    // and Schannel fails it too (see ServerTlsHandshakeHttpsProxyTests); macOS is not pinned.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public async Task UpgradeToTlsAsync_DnsOverTlsClientOffersOnlyH2_ThrowsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(
            serverStream, Local, Remote, new DuplexTransportControl(serverStream), ServerTlsHandshake.ForListener(settings, DnsOverTls));
        await using var client = new SslStream(clientStream, leaveInnerStreamOpen: true);

        var upgrade = connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        var clientHandshake = RunRefusedClientHandshakeAsync(client, ClientOptions("h2"), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => upgrade);
        Assert.IsNull(connection.TlsSession);
        await connection.DisposeAsync();
        await clientHandshake;
    }

    private static SslClientAuthenticationOptions ClientOptions(string offered) =>
        new()
        {
            TargetHost = "localhost",
            ApplicationProtocols = offered.Length == 0
                ? null
                : offered.Split(',').Select(protocol => new SslApplicationProtocol(protocol)).ToList(),
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        };

    private static async Task RunRefusedClientHandshakeAsync(SslStream client, SslClientAuthenticationOptions options, CancellationToken cancellationToken)
    {
        try
        {
            await client.AuthenticateAsClientAsync(options, cancellationToken);
            _ = await client.ReadAsync(new byte[1], cancellationToken);
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException or ObjectDisposedException)
        {
            // Expected: the server refused the handshake.
        }
    }

    private sealed class DuplexTransportControl(InMemoryDuplexStream stream) : IConnectionTransportControl
    {
        public void ShutdownSend() => stream.CompleteWrites();

        public void ResetAndClose() => stream.Dispose();
    }
}
