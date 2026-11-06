using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Protocol.Abstractions;
using Surl.Protocol.HttpProxy;

namespace Surl.Networking;

/// <summary>
/// The HTTPS proxy's TLS (ADR-0088 decision 6 and Amendment 2): a <see cref="StreamConnection"/>
/// given the handshake <see cref="ServerTlsHandshake.ForListener"/> makes for a proxy listen URL,
/// shaking hands with an <see cref="SslStream"/> client over an in-memory duplex pipe, so no
/// socket is opened, and then served by the real <see cref="HttpProxyProtocolServer"/>. The
/// request is the one pinned upstream curl 8.21.0 sent to an HTTPS proxy
/// (<c>Surl.Protocol.HttpProxy.UnitTests/Fixtures/https-proxy-forward</c>).
/// </summary>
[TestClass]
public sealed class ServerTlsHandshakeHttpsProxyTests
{
    private const string CurlsRequest =
        "GET http://origin.test/a?b HTTP/1.1\r\nHost: origin.test\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 3129);
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);
    private static readonly FixedTimeProvider Time = new(TestCertificates.Now);
    private static readonly ListenUrl HttpsProxy = new("https", "127.0.0.1", 3129) { IsProxy = true };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ForListener_HttpsProxy_OffersExactlyHttp11()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);

        var handshake = ServerTlsHandshake.ForListener(settings, HttpsProxy);

        CollectionAssert.AreEqual(new[] { SslApplicationProtocol.Http11 }, handshake!.ApplicationProtocols.ToArray());
    }

    [TestMethod]
    [DataRow("h2,http/1.1")]
    [DataRow("http/1.1")]
    public async Task ServeAsync_HttpsProxyHandshakeLikeCurl_AgreesHttp11AndProxiesOneRequest(string offered)
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(
            serverStream, Local, Remote, new DuplexTransportControl(serverStream), ServerTlsHandshake.ForListener(settings, HttpsProxy));
        var origin = new InMemoryConnection([Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok")]);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var log = new RecordingExchangeLog();
        await using var client = new SslStream(clientStream, leaveInnerStreamOpen: true);

        var upgrade = connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        await client.AuthenticateAsClientAsync(ClientOptions(offered), TestContext.CancellationToken);
        var session = await upgrade;
        var serving = new HttpProxyProtocolServer(origins).ServeAsync(connection, Context(log));
        await client.WriteAsync(Encoding.ASCII.GetBytes(CurlsRequest), TestContext.CancellationToken);
        var response = await ReadThroughBodyAsync(client, "ok");
        await client.ShutdownAsync();
        clientStream.CompleteWrites();
        await serving;

        Assert.AreEqual("http/1.1", session.ApplicationProtocol);
        Assert.AreEqual(SslApplicationProtocol.Http11, client.NegotiatedApplicationProtocol);
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", response);
        Assert.EndsWith("Via: 1.1 surl\r\n\r\nok", response);
        Assert.AreEqual(
            "GET /a?b HTTP/1.1\r\nHost: origin.test\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nVia: 1.1 surl\r\n\r\n",
            Encoding.ASCII.GetString(origin.WrittenBytes));
        CollectionAssert.Contains(
            log.Notes.ToList(),
            $"HTTPS proxy: curl's connection to the proxy is TLS ({session.Protocol}, ALPN http/1.1); requests are read inside it.");
    }

    // With ALPN offered and none in common, OpenSSL ends the handshake with
    // no_application_protocol (ADR-0010 section 6) and Schannel fails it too ("No common
    // application protocol exists", measured by BL-554; ADR-0088 Amendment 2), so the engine
    // notes "TLS handshake failed" and never serves the connection. macOS is not pinned.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public async Task UpgradeToTlsAsync_HttpsProxyAndClientOffersOnlyH2_ThrowsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(
            serverStream, Local, Remote, new DuplexTransportControl(serverStream), ServerTlsHandshake.ForListener(settings, HttpsProxy));
        await using var client = new SslStream(clientStream, leaveInnerStreamOpen: true);

        var upgrade = connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        var clientHandshake = RunRefusedClientHandshakeAsync(client, ClientOptions("h2"), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => upgrade);
        Assert.IsNull(connection.TlsSession);
        await connection.DisposeAsync();
        await clientHandshake;
    }

    // ADR-0010 section 2: a handshake that is not TLS at all - plain HTTP sent to the HTTPS
    // proxy - is TlsHandshakeException, the connection unusable and nothing written to it.
    [TestMethod]
    public async Task UpgradeToTlsAsync_HttpsProxyAndClientSendsPlainHttp_ThrowsTlsHandshakeExceptionAndWritesNothing()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        var control = new DuplexTransportControl(serverStream);
        var connection = new StreamConnection(
            serverStream, Local, Remote, control, ServerTlsHandshake.ForListener(settings, HttpsProxy));
        await clientStream.WriteAsync(Encoding.ASCII.GetBytes(CurlsRequest), TestContext.CancellationToken);
        clientStream.CompleteWrites();

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        Assert.IsNull(connection.TlsSession);
        await connection.DisposeAsync();
        Assert.AreEqual(0, control.ShutdownSendCount);
        Assert.AreEqual(1, serverStream.DisposeCount);
        await clientStream.DisposeAsync();
    }

    private static SslClientAuthenticationOptions ClientOptions(string offered) =>
        new()
        {
            TargetHost = "localhost",
            ApplicationProtocols = offered.Split(',').Select(protocol => new SslApplicationProtocol(protocol)).ToList(),
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        };

    private static ExchangeContext Context(RecordingExchangeLog log) =>
        new(1, HttpsProxy, Local, Remote, log, Time, CancellationToken.None);

    private async Task<string> ReadThroughBodyAsync(SslStream client, string bodyEnd)
    {
        var received = new StringBuilder();
        var buffer = new byte[256];
        while (!received.ToString().EndsWith(bodyEnd, StringComparison.Ordinal))
        {
            var count = await client.ReadAsync(buffer, TestContext.CancellationToken);
            Assert.AreNotEqual(0, count);
            received.Append(Encoding.ASCII.GetString(buffer, 0, count));
        }

        return received.ToString();
    }

    // The client's view of a refused handshake differs by TLS version; either way it ends.
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
        public int ShutdownSendCount { get; private set; }

        public void ShutdownSend()
        {
            ShutdownSendCount++;
            stream.CompleteWrites();
        }

        public void ResetAndClose() => stream.Dispose();
    }
}
