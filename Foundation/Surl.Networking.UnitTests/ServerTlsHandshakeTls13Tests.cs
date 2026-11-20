using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Protocol.Abstractions;
using Surl.Tls13Stream;

namespace Surl.Networking;

/// <summary>
/// Which stream serves a handshake when <see cref="ITls13Support"/> says <see cref="SslStream"/> has no
/// TLS 1.3 server (ADR-0157, decisions 1 to 3): real <see cref="SslStream"/> clients over an in-memory
/// duplex pipe. The TLS 1.3-only <see cref="SslStream"/> client cannot run on macOS, whose own
/// <see cref="SslStream"/> is the gap being worked around.
/// </summary>
[TestClass]
public sealed class ServerTlsHandshakeTls13Tests
{
    private static readonly FixedTimeProvider Time = new(TestCertificates.Now);
    private static readonly ListenUrl Https = new("https", "127.0.0.1", 443);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PlatformTls13Support_IsMacOnly() =>
        Assert.AreEqual(OperatingSystem.IsMacOS(), new PlatformTls13Support().SslStreamLacksTls13Server);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsync_Tls13OnlyOnAPlatformWithoutTls13_IsServedByTheHandBuiltStream()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);

        var (server, session, client) = await HandshakeAsync(settings, lacksTls13: true, ClientOptions(SslProtocols.Tls13));
        await using (client)
        await using (server)
        {
            Assert.IsInstanceOfType<Tls13ServerStream>(server);
            Assert.AreEqual(SslProtocols.Tls13, session.Protocol);
            Assert.AreEqual(client.NegotiatedCipherSuite, session.CipherSuite);
            Assert.AreEqual("http/1.1", session.ApplicationProtocol);
            Assert.AreEqual("localhost", session.ServerName);
            Assert.IsNull(session.ClientCertificate);

            await client.WriteAsync(Encoding.ASCII.GetBytes("ping"), TestContext.CancellationToken);
            var request = new byte[8];
            Assert.AreEqual(4, await server.ReadAsync(request, TestContext.CancellationToken));
            await server.WriteAsync(Encoding.ASCII.GetBytes("pong"), TestContext.CancellationToken);
            var response = new byte[8];
            Assert.AreEqual(4, await client.ReadAsync(response, TestContext.CancellationToken));
            Assert.AreEqual("pong", Encoding.ASCII.GetString(response, 0, 4));
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsync_Tls13OnlyWithRsaCertificateAndIntermediate_IsServedByTheHandBuiltStream()
    {
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var certificate = TestCertificates.CreateRsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [authority], [], Time);

        var (server, _, client) = await HandshakeAsync(settings, lacksTls13: true, ClientOptions(SslProtocols.Tls13));
        await using (client)
        await using (server)
        {
            Assert.IsInstanceOfType<Tls13ServerStream>(server);
            Assert.AreEqual(certificate.Thumbprint, client.RemoteCertificate!.GetCertHashString());
        }
    }

    [TestMethod]
    public async Task AuthenticateAsync_ClientAlsoOffersTls12_IsServedBySslStream()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);

        var (server, session, client) = await HandshakeAsync(settings, lacksTls13: true, ClientOptions(SslProtocols.Tls12 | SslProtocols.Tls13));
        await using (client)
        await using (server)
        {
            Assert.IsInstanceOfType<SslStream>(server);
            Assert.IsTrue(session.Protocol is SslProtocols.Tls12 or SslProtocols.Tls13);
        }
    }

    [TestMethod]
    public async Task AuthenticateAsync_Tls12OnlyClient_IsServedBySslStream()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);

        var (server, session, client) = await HandshakeAsync(settings, lacksTls13: true, ClientOptions(SslProtocols.Tls12));
        await using (client)
        await using (server)
        {
            Assert.IsInstanceOfType<SslStream>(server);
            Assert.AreEqual(SslProtocols.Tls12, session.Protocol);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsync_Tls13OnlyOnAPlatformWithTls13_IsServedBySslStream()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);

        var (server, session, client) = await HandshakeAsync(settings, lacksTls13: false, ClientOptions(SslProtocols.Tls13));
        await using (client)
        await using (server)
        {
            Assert.IsInstanceOfType<SslStream>(server);
            Assert.AreEqual(SslProtocols.Tls13, session.Protocol);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsync_Tls13OnlyWithClientCertificateSetting_IsServedByTheHandBuiltStreamWithTheVerifiedCertificate()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var clientCertificate = TestCertificates.CreateClientCertificate(authority);
        using var settings = new ServerTlsSettings(certificate, [], [authority], Time);
        var options = ClientOptions(SslProtocols.Tls13);
        options.ClientCertificates = [clientCertificate];
        options.LocalCertificateSelectionCallback = (_, _, _, _, _) => clientCertificate;

        var (server, session, client) = await HandshakeAsync(settings, lacksTls13: true, options);
        await using (client)
        await using (server)
        {
            Assert.IsInstanceOfType<Tls13ServerStream>(server);
            Assert.AreEqual(SslProtocols.Tls13, session.Protocol);
            Assert.AreEqual(clientCertificate.Thumbprint, session.ClientCertificate!.Thumbprint);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsync_Tls13OnlyWithClientCertificateSettingAndNoCertificateSent_IsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var settings = new ServerTlsSettings(certificate, [], [authority], Time);

        await AssertClientRefusedAsync(settings, ClientOptions(SslProtocols.Tls13));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsync_Tls13OnlyWithClientCertificateSettingAndAnUntrustedCertificate_IsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var stranger = TestCertificates.CreateCertificateAuthority("CN=stranger CA");
        using var clientCertificate = TestCertificates.CreateClientCertificate(stranger);
        using var settings = new ServerTlsSettings(certificate, [], [authority], Time);
        var options = ClientOptions(SslProtocols.Tls13);
        options.ClientCertificates = [clientCertificate];
        options.LocalCertificateSelectionCallback = (_, _, _, _, _) => clientCertificate;

        await AssertClientRefusedAsync(settings, options);
    }

    private async Task AssertClientRefusedAsync(ServerTlsSettings settings, SslClientAuthenticationOptions clientOptions)
    {
        var handshake = new ServerTlsHandshake(settings, [], new FakeTls13Support(true));
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        var client = new SslStream(clientStream, leaveInnerStreamOpen: true);
        var clientHandshake = client.AuthenticateAsClientAsync(clientOptions, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => handshake.AuthenticateAsync(serverStream, TestContext.CancellationToken));

        await client.DisposeAsync();
        try
        {
            await clientHandshake;
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException or ObjectDisposedException)
        {
            // A TLS 1.3 client may or may not see the refusal before its own handshake ends.
        }
    }

    [TestMethod]
    public async Task AuthenticateAsync_Tls13OnlyHelloAndTheClientVanishes_IsTlsHandshakeExceptionAfterTheServerHello()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var handshake = new ServerTlsHandshake(settings, [], new FakeTls13Support(true));
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        var hello = ClientHelloBytes.Record(22, ClientHelloBytes.Message([0x0304]));

        await clientStream.WriteAsync(hello, TestContext.CancellationToken);
        clientStream.CompleteWrites();
        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => handshake.AuthenticateAsync(serverStream, TestContext.CancellationToken));

        var reply = new byte[1];
        Assert.AreEqual(1, await clientStream.ReadAsync(reply, TestContext.CancellationToken));
        Assert.AreEqual(22, reply[0]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_Tls13OnlyHelloWithNoSharedCipherSuite_IsTlsHandshakeExceptionAfterAFatalAlert()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var handshake = new ServerTlsHandshake(settings, [], new FakeTls13Support(true));
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        var hello = ClientHelloBytes.Record(22, ClientHelloBytes.Message([0x0304], cipherSuite: 0x1399));

        await clientStream.WriteAsync(hello, TestContext.CancellationToken);
        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => handshake.AuthenticateAsync(serverStream, TestContext.CancellationToken));

        var reply = new byte[1];
        Assert.AreEqual(1, await clientStream.ReadAsync(reply, TestContext.CancellationToken));
        Assert.AreEqual(21, reply[0]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_Tls13OnlyListenerAndHelloOffersTls12And13_IsServedByTheHandBuiltStream()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time) { AcceptedVersions = new TlsVersionRange(SslProtocols.Tls13, SslProtocols.Tls13) };
        var handshake = new ServerTlsHandshake(settings, [], new FakeTls13Support(true));
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        var hello = ClientHelloBytes.Record(22, ClientHelloBytes.Message([0x0303, 0x0304]));

        await clientStream.WriteAsync(hello, TestContext.CancellationToken);
        clientStream.CompleteWrites();
        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => handshake.AuthenticateAsync(serverStream, TestContext.CancellationToken));

        var reply = new byte[1];
        Assert.AreEqual(1, await clientStream.ReadAsync(reply, TestContext.CancellationToken));
        Assert.AreEqual(22, reply[0], "The hand-built stream answered with a ServerHello record.");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsync_Tls13OnlyListenerAndClientOffersTls12And13_CompletesAsTls13OnTheHandBuiltStream()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time) { AcceptedVersions = new TlsVersionRange(SslProtocols.Tls13, SslProtocols.Tls13) };

        var (server, session, client) = await HandshakeAsync(settings, lacksTls13: true, ClientOptions(SslProtocols.Tls12 | SslProtocols.Tls13));
        await using (client)
        await using (server)
        {
            Assert.IsInstanceOfType<Tls13ServerStream>(server);
            Assert.AreEqual(SslProtocols.Tls13, session.Protocol);
        }
    }

    [TestMethod]
    public async Task AuthenticateAsync_Tls13OnlyListenerAndHelloOffersTls12Only_SendsProtocolVersionAlert()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time) { AcceptedVersions = new TlsVersionRange(SslProtocols.Tls13, SslProtocols.Tls13) };
        var handshake = new ServerTlsHandshake(settings, [], new FakeTls13Support(true));
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();

        await clientStream.WriteAsync(ClientHelloBytes.Record(22, ClientHelloBytes.Message([0x0303])), TestContext.CancellationToken);
        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => handshake.AuthenticateAsync(serverStream, TestContext.CancellationToken));

        var reply = new byte[7];
        Assert.AreEqual(7, await clientStream.ReadAtLeastAsync(reply, 7, cancellationToken: TestContext.CancellationToken));
        CollectionAssert.AreEqual(new byte[] { 21, 3, 3, 0, 2, 2, 70 }, reply);
    }

    [TestMethod]
    public async Task AuthenticateAsync_Tls13OnlyListenerAndBytesAreNoClientHello_GoesToSslStreamWhichRefuses()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time) { AcceptedVersions = new TlsVersionRange(SslProtocols.Tls13, SslProtocols.Tls13) };
        var handshake = new ServerTlsHandshake(settings, [], new FakeTls13Support(true));
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();

        await clientStream.WriteAsync("GET / HTTP/1.1\r\n\r\n"u8.ToArray(), TestContext.CancellationToken);
        clientStream.CompleteWrites();

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => handshake.AuthenticateAsync(serverStream, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AuthenticateAsync_Tls13OnlyListenerRefusesAndTheClientIsGone_StillThrowsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time) { AcceptedVersions = new TlsVersionRange(SslProtocols.Tls13, SslProtocols.Tls13) };
        var handshake = new ServerTlsHandshake(settings, [], new FakeTls13Support(true));
        var hello = ClientHelloBytes.Record(22, ClientHelloBytes.Message([0x0303]));

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => handshake.AuthenticateAsync(new ScriptedReadResettingWriteStream(hello), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AuthenticateAsync_ClientThatSendsNothingOnAPlatformWithoutTls13_IsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var handshake = new ServerTlsHandshake(settings, [], new FakeTls13Support(true));
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();

        clientStream.CompleteWrites();

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => handshake.AuthenticateAsync(serverStream, TestContext.CancellationToken));
    }

    private async Task<(Stream Server, TlsSession Session, SslStream Client)> HandshakeAsync(
        ServerTlsSettings settings, bool lacksTls13, SslClientAuthenticationOptions clientOptions)
    {
        var handshake = new ServerTlsHandshake(settings, TlsApplicationProtocols.ForListener(Https), new FakeTls13Support(lacksTls13));
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        var server = handshake.AuthenticateAsync(serverStream, TestContext.CancellationToken);
        var client = new SslStream(clientStream, leaveInnerStreamOpen: true);
        await client.AuthenticateAsClientAsync(clientOptions, TestContext.CancellationToken);
        var (stream, session) = await server;

        return (stream, session, client);
    }

    private static SslClientAuthenticationOptions ClientOptions(SslProtocols protocols) =>
        new()
        {
            TargetHost = "localhost",
            EnabledSslProtocols = protocols,
            ApplicationProtocols = [SslApplicationProtocol.Http11],
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        };

    [TestMethod]
    public async Task AuthenticateAsync_TransportResetWhileReadingTheHelloOnTheHandBuiltPath_ThrowsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var handshake = new ServerTlsHandshake(settings, [SslApplicationProtocol.Http11], new FakeTls13Support(true));

        var failure = await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => handshake.AuthenticateAsync(new ResettingReadStream(), TestContext.CancellationToken));

        Assert.IsInstanceOfType<IOException>(failure.InnerException);
    }

    private sealed record FakeTls13Support(bool SslStreamLacksTls13Server) : ITls13Support;

    private sealed class ScriptedReadResettingWriteStream(byte[] toRead) : Stream
    {
        private readonly MemoryStream source = new(toRead);

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
            source.ReadAsync(buffer, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("The peer reset the connection."));

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ResettingReadStream : Stream
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
}
