using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Server-side TLS on a <see cref="StreamConnection"/> (ADR-0010): real <see cref="SslStream"/>
/// handshakes over an in-memory duplex pipe, with an <see cref="SslStream"/> client in the test
/// and certificates made by <see cref="CertificateRequest"/>, so no socket is opened.
/// </summary>
[TestClass]
public sealed class StreamConnectionTlsTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 443);
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);
    private static readonly FixedTimeProvider Time = new(TestCertificates.Now);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UpgradeToTlsAsync_EcdsaCertificate_CompletesAndExchangesBytesBothWays()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        var (session, client) = await pair.HandshakeAsync(ClientOptions(), TestContext.CancellationToken);
        await using (client)
        {
            AssertDefaultVersion(session.Protocol);
            Assert.AreSame(session, pair.Connection.TlsSession);
            Assert.IsNull(session.ClientCertificate);

            await client.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n"), TestContext.CancellationToken);
            var request = new byte[64];
            var requestCount = await pair.Connection.ReadAsync(request, TestContext.CancellationToken);
            Assert.AreEqual("GET / HTTP/1.1\r\n", Encoding.ASCII.GetString(request, 0, requestCount));

            await pair.Connection.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n"), TestContext.CancellationToken);
            var response = new byte[64];
            var responseCount = await client.ReadAsync(response, TestContext.CancellationToken);
            Assert.AreEqual("HTTP/1.1 200 OK\r\n", Encoding.ASCII.GetString(response, 0, responseCount));
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_RsaCertificate_Completes()
    {
        using var certificate = TestCertificates.CreateRsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        var (session, client) = await pair.HandshakeAsync(ClientOptions(), TestContext.CancellationToken);
        await using (client)
        {
            AssertDefaultVersion(session.Protocol);
            Assert.AreEqual(certificate.Thumbprint, client.RemoteCertificate!.GetCertHashString());
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task UpgradeToTlsAsync_ClientAsksForTls13Only_NegotiatesTls13()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");
        var options = ClientOptions();
        options.EnabledSslProtocols = SslProtocols.Tls13;

        var (session, client) = await pair.HandshakeAsync(options, TestContext.CancellationToken);
        await using (client)
        {
            Assert.AreEqual(SslProtocols.Tls13, session.Protocol);
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_ClientSendsServerName_ReportsIt()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        var (session, client) = await pair.HandshakeAsync(ClientOptions(), TestContext.CancellationToken);
        await using (client)
        {
            Assert.AreEqual("localhost", session.ServerName);
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_ClientConnectsToAnIpLiteral_ReportsNoServerName()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");
        var options = ClientOptions();
        options.TargetHost = "127.0.0.1";

        var (session, client) = await pair.HandshakeAsync(options, TestContext.CancellationToken);
        await using (client)
        {
            Assert.IsNull(session.ServerName);
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_TransportResetDuringHandshake_ThrowsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var (server, _) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(
            new ResetStream(), Local, Remote, new DuplexTransportControl(server), ServerTlsHandshake.ForListener(settings, new ListenUrl("https", "127.0.0.1", 443)));

        var failure = await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        Assert.IsInstanceOfType<IOException>(failure.InnerException);
    }

    [TestMethod]
    [DataRow("https")]
    [DataRow("wss")]
    public async Task UpgradeToTlsAsync_HttpSchemeAndClientOffersHttp11_AgreesOnHttp11(string scheme)
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, scheme);
        var options = ClientOptions();
        options.ApplicationProtocols = [SslApplicationProtocol.Http11];

        var (session, client) = await pair.HandshakeAsync(options, TestContext.CancellationToken);
        await using (client)
        {
            Assert.AreEqual("http/1.1", session.ApplicationProtocol);
            Assert.AreEqual(SslApplicationProtocol.Http11, client.NegotiatedApplicationProtocol);
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_ClientOffersHttp10AndHttp11LikeCurlHttp10_AgreesOnHttp11()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");
        var options = ClientOptions();
        options.ApplicationProtocols = [new SslApplicationProtocol("http/1.0"), SslApplicationProtocol.Http11];

        var (session, client) = await pair.HandshakeAsync(options, TestContext.CancellationToken);
        await using (client)
        {
            Assert.AreEqual("http/1.1", session.ApplicationProtocol);
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_ClientOffersNoAlpn_ServesWithoutIt()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        var (session, client) = await pair.HandshakeAsync(ClientOptions(), TestContext.CancellationToken);
        await using (client)
        {
            Assert.IsNull(session.ApplicationProtocol);
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_NonHttpSchemeAndClientOffersHttp11_AgreesOnNothing()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "ftp");
        var options = ClientOptions();
        options.ApplicationProtocols = [SslApplicationProtocol.Http11];

        var (session, client) = await pair.HandshakeAsync(options, TestContext.CancellationToken);
        await using (client)
        {
            Assert.IsNull(session.ApplicationProtocol);
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_ClientCertificateSignedByTrustAnchor_IsAcceptedAndReported()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var clientCertificate = TestCertificates.CreateClientCertificate(authority);
        using var settings = new ServerTlsSettings(certificate, [], [authority], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        var (session, client) = await pair.HandshakeAsync(ClientOptions(clientCertificate), TestContext.CancellationToken);
        await using (client)
        {
            Assert.AreEqual(clientCertificate.Thumbprint, session.ClientCertificate!.Thumbprint);
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_ClientCertificateSignedByAStranger_FailsTheHandshake()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var stranger = TestCertificates.CreateCertificateAuthority("CN=stranger CA");
        using var clientCertificate = TestCertificates.CreateClientCertificate(stranger);
        using var settings = new ServerTlsSettings(certificate, [], [authority], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        await pair.AssertHandshakeFailsAsync(ClientOptions(clientCertificate), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_VerificationRequiredAndNoClientCertificate_FailsTheHandshake()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var settings = new ServerTlsSettings(certificate, [], [authority], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        await pair.AssertHandshakeFailsAsync(ClientOptions(), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_ClientSendsGarbage_ThrowsTlsHandshakeExceptionAndLeavesTheConnectionUnusable()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        await pair.ClientStream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n"), TestContext.CancellationToken);
        pair.ClientStream.CompleteWrites();

        var failure = await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => pair.Connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        Assert.IsNotNull(failure.InnerException);
        Assert.IsNull(pair.Connection.TlsSession);
        await Assert.ThrowsExactlyAsync<IOException>(
            () => pair.Connection.ReadAsync(new byte[1], TestContext.CancellationToken).AsTask());
        await pair.Connection.DisposeAsync();
        Assert.AreEqual(0, pair.TransportControl.ShutdownSendCount);
        Assert.AreEqual(1, pair.ServerStream.DisposeCount);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_PeerClosesBeforeHello_ThrowsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        pair.ClientStream.CompleteWrites();

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => pair.Connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_CancelledWhileWaitingForHello_ThrowsAndLeavesTheConnectionUnusable()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var upgrade = pair.Connection.UpgradeToTlsAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => upgrade);
        await Assert.ThrowsExactlyAsync<IOException>(
            () => pair.Connection.WriteAsync(new byte[1], TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_AbortedWhileWaitingForHello_ThrowsIOException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");

        var upgrade = pair.Connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        pair.Connection.Abort();

        var failure = await Assert.ThrowsAsync<IOException>(() => upgrade);
        Assert.AreEqual(1, pair.TransportControl.ResetAndCloseCount);
        Assert.IsNotNull(failure);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_Secured_SendsCloseNotifyThenFin()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");
        var (_, client) = await pair.HandshakeAsync(ClientOptions(), TestContext.CancellationToken);

        await using (client)
        {
            await pair.Connection.CompleteWritesAsync(TestContext.CancellationToken);

            Assert.AreEqual(0, await client.ReadAsync(new byte[16], TestContext.CancellationToken));
            Assert.AreEqual(1, pair.TransportControl.ShutdownSendCount);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_Secured_ClosesGracefullyAndDisposesTheTransport()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");
        var (_, client) = await pair.HandshakeAsync(ClientOptions(), TestContext.CancellationToken);

        await using (client)
        {
            await pair.Connection.DisposeAsync();

            Assert.AreEqual(0, await client.ReadAsync(new byte[16], TestContext.CancellationToken));
            Assert.AreEqual(1, pair.TransportControl.ShutdownSendCount);
            Assert.AreEqual(1, pair.ServerStream.DisposeCount);
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_NoTlsSettings_Throws()
    {
        var (server, _) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(server, Local, Remote, new DuplexTransportControl(server));

        Assert.IsNull(connection.TlsSession);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_AlreadySecured_Throws()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "https");
        var (_, client) = await pair.HandshakeAsync(ClientOptions(), TestContext.CancellationToken);

        await using (client)
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => pair.Connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());
        }
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_AfterCompleteWrites_Throws()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "ftp");

        await pair.Connection.CompleteWritesAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => pair.Connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_ReadPending_Throws()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        await using var pair = ConnectionPair.Create(settings, "ftp");
        var read = pair.Connection.ReadAsync(new byte[16], TestContext.CancellationToken).AsTask();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => pair.Connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        pair.ClientStream.CompleteWrites();
        Assert.AreEqual(0, await read);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_WritePending_Throws()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var (server, _) = InMemoryDuplexStream.CreatePair();
        var stream = new WaitingWriteStream(server);
        await using var connection = new StreamConnection(
            stream, Local, Remote, new DuplexTransportControl(server), ServerTlsHandshake.ForListener(settings, new ListenUrl("ftp", "127.0.0.1", 21)));
        var write = connection.WriteAsync(new byte[1], TestContext.CancellationToken).AsTask();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        stream.ReleaseWrite();
        await write;
    }

    private static void AssertDefaultVersion(SslProtocols protocol) =>
        Assert.IsTrue(protocol is SslProtocols.Tls12 or SslProtocols.Tls13, $"Negotiated {protocol}.");

    private static SslClientAuthenticationOptions ClientOptions(X509Certificate2? clientCertificate = null) =>
        new()
        {
            TargetHost = "localhost",
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
            ClientCertificates = clientCertificate is null ? null : [clientCertificate],
            LocalCertificateSelectionCallback = clientCertificate is null ? null : (_, _, _, _, _) => clientCertificate,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        };

    private sealed class ConnectionPair : IAsyncDisposable
    {
        private ConnectionPair(InMemoryDuplexStream serverStream, InMemoryDuplexStream clientStream, StreamConnection connection, DuplexTransportControl transportControl)
        {
            ServerStream = serverStream;
            ClientStream = clientStream;
            Connection = connection;
            TransportControl = transportControl;
        }

        public InMemoryDuplexStream ServerStream { get; }

        public InMemoryDuplexStream ClientStream { get; }

        public StreamConnection Connection { get; }

        public DuplexTransportControl TransportControl { get; }

        public static ConnectionPair Create(ServerTlsSettings settings, string scheme)
        {
            var (server, client) = InMemoryDuplexStream.CreatePair();
            var control = new DuplexTransportControl(server);
            var handshake = ServerTlsHandshake.ForListener(settings, new ListenUrl(scheme, "127.0.0.1", 443));
            var connection = new StreamConnection(server, Local, Remote, control, handshake);

            return new ConnectionPair(server, client, connection, control);
        }

        public async Task<(TlsSession Session, SslStream Client)> HandshakeAsync(
            SslClientAuthenticationOptions options, CancellationToken cancellationToken)
        {
            var upgrade = Connection.UpgradeToTlsAsync(cancellationToken).AsTask();
            var client = new SslStream(ClientStream, leaveInnerStreamOpen: true);
            await client.AuthenticateAsClientAsync(options, cancellationToken);

            return (await upgrade, client);
        }

        public async Task AssertHandshakeFailsAsync(SslClientAuthenticationOptions options, CancellationToken cancellationToken)
        {
            var upgrade = Connection.UpgradeToTlsAsync(cancellationToken).AsTask();
            await using var client = new SslStream(ClientStream, leaveInnerStreamOpen: true);
            var clientHandshake = RunClientHandshakeAsync(client, options, cancellationToken);

            await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => upgrade);
            Assert.IsNull(Connection.TlsSession);

            // Closing the server side ends whatever the client is still waiting for.
            await Connection.DisposeAsync();
            await clientHandshake;
        }

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
            await ClientStream.DisposeAsync();
        }

        // The client's view of a rejected handshake differs by platform and TLS version: under
        // TLS 1.3 its handshake completes before the server rejects its certificate, so its
        // first read is where the rejection shows. Either way it ends in an exception or EOF.
        private static async Task RunClientHandshakeAsync(SslStream client, SslClientAuthenticationOptions options, CancellationToken cancellationToken)
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
    }

    private sealed class DuplexTransportControl(InMemoryDuplexStream stream) : IConnectionTransportControl
    {
        public int ShutdownSendCount { get; private set; }

        public int ResetAndCloseCount { get; private set; }

        public void ShutdownSend()
        {
            ShutdownSendCount++;
            stream.CompleteWrites();
        }

        public void ResetAndClose()
        {
            ResetAndCloseCount++;
            stream.Dispose();
        }
    }

    private sealed class ResetStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("The peer reset the connection."));

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class WaitingWriteStream(Stream inner) : Stream
    {
        private readonly TaskCompletionSource writeReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void ReleaseWrite() => writeReleased.SetResult();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await writeReleased.Task;
            await inner.WriteAsync(buffer, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
