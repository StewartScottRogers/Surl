using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Surl.Content;
using Surl.Cookies;
using Surl.Protocol.Abstractions;
using Surl.Quic;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The HTTP/3 server <c>surl --http3</c> registers for https's UDP side (ADR-0082 decisions 5 and
/// 6), driven from pinned upstream curl's recorded Initials by <see cref="Http3TestClient"/>.
/// </summary>
[TestClass]
public sealed class Http3ProtocolServerTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public void CreateRedactor_ReturnsTheHttp3RedactorThatWithholdsAuthorization()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var server = new Http3ProtocolServer(Store(), new AnonymousAuthenticationPolicy(), [], key, TimeSpan.Zero);

        var redactor = server.CreateRedactor(new ExchangeRedactionContext(1, "https"));
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        var http3Redactor = Assert.IsInstanceOfType<Surl.Http3.Http3RequestBytesRedactor>(redactor);
        http3Redactor.RedactDecodedText(":method: GET\r\nauthorization: Bearer tok-h3\r\n\r\n"u8, output);

        Assert.AreEqual(":method: GET\r\nauthorization: ****\r\n\r\n", System.Text.Encoding.UTF8.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void Schemes_IsHttpsOnly()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var server = new Http3ProtocolServer(Store(), new AnonymousAuthenticationPolicy(), [], key, TimeSpan.Zero);

        CollectionAssert.AreEqual(new[] { "https" }, server.Schemes.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RequiresClientCertificate_IsWhetherATrustCheckWasGiven(bool givesTrustCheck)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Func<IReadOnlyList<ReadOnlyMemory<byte>>, bool>? trusts = givesTrustCheck ? _ => true : null;

        var server = new Http3ProtocolServer(Store(), new AnonymousAuthenticationPolicy(), [], key, TimeSpan.Zero, trusts);

        Assert.AreEqual(givesTrustCheck, server.RequiresClientCertificate);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var policy = new AnonymousAuthenticationPolicy();

        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ProtocolServer(null!, policy, [], key, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ProtocolServer(Store(), null!, [], key, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ProtocolServer(Store(), policy, null!, key, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ProtocolServer(Store(), policy, [], null!, TimeSpan.Zero));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var server = new Http3ProtocolServer(Store(), new AnonymousAuthenticationPolicy(), [], key, TimeSpan.Zero);
        var context = Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), Token);
        await using var flow = new InMemoryDatagramFlow(new byte[1], []);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(flow, null!));
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_RecordedCurlInitial_SendsOneInitialHoldingConnectionCloseConnectionRefused(ConnectionRefusal refusal)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var server = new Http3ProtocolServer(Store(), new AnonymousAuthenticationPolicy(), [], key, TimeSpan.Zero);
        var first = RecordedCurlFirstDatagram();
        await using var flow = new InMemoryDatagramFlow(first);

        await server.WriteRefusalAsync(flow, refusal, Token);

        var sent = flow.SentDatagrams.Single();
        var destinationConnectionId = first.AsSpan(6, first[5]).ToArray();
        var sourceConnectionId = first.AsSpan(7 + first[5], first[6 + first[5]]).ToArray();
        var (client, serverKeys) = QuicInitialSecrets.DerivePacketProtection(destinationConnectionId);
        using (client)
        using (serverKeys)
        {
            var opened = serverKeys.Unprotect(sent, 0, 0);
            Assert.AreEqual(QuicUnprotectStatus.Unprotected, opened.Status);
            var packet = (QuicLongHeaderPacket)opened.Packet!;
            Assert.AreEqual(QuicPacketType.Initial, packet.Type);
            CollectionAssert.AreEqual(sourceConnectionId, packet.DestinationConnectionId.ToArray());
            CollectionAssert.AreEqual(new byte[] { 0x1c, 0x02, 0x00, 0x00 }, packet.Payload.ToArray());
            var close = (QuicConnectionCloseFrame)QuicFrameReader.Read(packet.Payload, QuicPacketType.Initial).Single();
            Assert.AreEqual(0x02UL, close.ErrorCode);
        }
    }

    [TestMethod]
    public async Task WriteRefusalAsync_FirstDatagramNotAVersion1Initial_SendsNoBytes()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var server = new Http3ProtocolServer(Store(), new AnonymousAuthenticationPolicy(), [], key, TimeSpan.Zero);
        var first = RecordedCurlFirstDatagram();
        first[4] = 0x02;
        await using var flow = new InMemoryDatagramFlow(first);

        await server.WriteRefusalAsync(flow, ConnectionRefusal.TooManyConnections, Token);

        Assert.IsEmpty(flow.SentDatagrams);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullFlow_Throws()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var server = new Http3ProtocolServer(Store(), new AnonymousAuthenticationPolicy(), [], key, TimeSpan.Zero);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, Token).AsTask());
    }

    // The first datagram pinned upstream curl 8.21.0 sent for an h3 request (Fixtures/h3-client-initial),
    // stored with each datagram prefixed by its two-byte big-endian length.
    private static byte[] RecordedCurlFirstDatagram()
    {
        var bytes = RecordedFixture.ReadBytes("h3-client-initial", "static-8.21.0-win.datagrams.bin");
        var length = (bytes[0] << 8) | bytes[1];
        return bytes[2..(2 + length)];
    }

    [TestMethod]
    public async Task ServeAsync_GetOfAContentStoreFile_AnswersItOverHttp3()
    {
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy() };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("200", response.Status);
        Assert.AreEqual(FileBody, response.BodyText);
        Assert.IsTrue(response.Fin);
    }

    [TestMethod]
    public async Task ServeAsync_ConfiguredCookies_AddEachSetCookieFieldToTheHttp3Reply()
    {
        using var client = new Http3TestClient
        {
            ProtocolServerPolicy = new AnonymousAuthenticationPolicy(),
            Cookies =
            [
                new CookieDefinition("sid", "abc123", Path: "/", MaxAge: 3600, Secure: true, HttpOnly: true),
                new CookieDefinition("sess", "xyz", Path: "/"),
                new CookieDefinition("other", "1", OnPath: "/elsewhere"),
            ],
        };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(
            new[] { "set-cookie: sid=abc123; Path=/; Max-Age=3600; Secure; HttpOnly", "set-cookie: sess=xyz; Path=/" },
            response.RegularFields.TakeLast(2).ToArray());
        Assert.HasCount(2, response.Fields.Where(line => line.Name == "set-cookie"));
    }

    [TestMethod]
    public async Task ServeAsync_RequestCookieFields_PublishesEachPairAsCookieReceivedEvents()
    {
        var events = new RecordingSurlEventPublisher();
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy(), Events = events };
        await client.StartAsync(Token);

        // RFC 9114 section 4.2.1 lets a client split its cookies over several cookie field lines.
        client.Send(0, client.Request("GET", "/file.txt", ("cookie", "sess=xyz; sid=abc123"), ("cookie", "a=1")), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        CollectionAssert.AreEqual(
            new[]
            {
                new CookieReceivedEvent(1, "http", "sess", "xyz"),
                new CookieReceivedEvent(1, "http", "sid", "abc123"),
                new CookieReceivedEvent(1, "http", "a", "1"),
            },
            events.Events.OfType<CookieReceivedEvent>().ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NoCookieConfigured_Http3ReplyIsUnchanged()
    {
        // Without a ProtocolServerPolicy the test client serves the bare answerer, with no cookie handling.
        using var bare = new Http3TestClient();
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy() };
        await bare.StartAsync(Token);
        await client.StartAsync(Token);

        bare.Send(0, bare.Request("GET", "/file.txt"), fin: true);
        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await bare.UntilAsync(() => bare.HasEnded(0), Token);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        CollectionAssert.AreEqual(bare.Response(0).Fields.ToArray(), client.Response(0).Fields.ToArray());
        Assert.AreEqual(bare.Response(0).BodyText, client.Response(0).BodyText);
        Assert.IsFalse(client.Response(0).Fields.Any(line => line.Name == "set-cookie"));
    }

    [TestMethod]
    public async Task ServeAsync_Request_ReportsTheRequestedPathToTheDecryptedBytesSink()
    {
        var sink = new UnitTestRecordingDecryptedBytesSink();
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy(), DecryptedBytes = sink };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.IsTrue(sink.HasReceived("/file.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeTheHandshakeCompletes_StopsTheConnection()
    {
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy() };
        var serving = client.Serve(Token);

        await client.Exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), Token));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelled_ClosesWithH3NoErrorAndReturns()
    {
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy() };
        await client.StartAsync(Token);
        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        await client.Exchange.CancelAsync();
        await client.FinishAsync(Token);

        Assert.AreEqual(0x100UL, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_Request_IsJudgedAsOnAnEncryptedConnectionWithTheTls13SessionQuicAgreed()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
        using var client = new Http3TestClient { ProtocolServerPolicy = policy };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var session = policy.StartedTlsSessions.Single();
        Assert.IsNotNull(session);
        Assert.AreEqual(SslProtocols.Tls13, session.Protocol);
        Assert.AreEqual((System.Net.Security.TlsCipherSuite)client.Quic.Tls.CipherSuite, session.CipherSuite);
        Assert.AreEqual("h3", session.ApplicationProtocol);
        Assert.IsNull(session.ClientCertificate);
    }

    [TestMethod]
    public async Task ServeAsync_TrustCheckAndATrustedClientCertificate_JudgesRequestsWithThatCertificate()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
        using var certificate = ClientCertificate();
        using var client = new Http3TestClient
        {
            ProtocolServerPolicy = policy,
            TrustsClientCertificateChain = chain => chain[0].Span.SequenceEqual(certificate.RawData),
        };
        client.Quic.Tls.ClientCertificate = certificate;
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.IsTrue(client.Quic.Tls.ReceivedCertificateRequest);
        Assert.AreEqual("200", client.Response(0).Status);
        CollectionAssert.AreEqual(certificate.RawData, policy.StartedTlsSessions.Single()!.ClientCertificate!.RawData);
    }

    [TestMethod]
    public async Task ServeAsync_NoHeadTimeout_StillCompletesTheHandshake()
    {
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy(), IdleTimeout = TimeSpan.Zero };
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan });

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual("200", client.Response(0).Status);
    }

    // A 1024-bit key keeps the client's Certificate, CertificateVerify and Finished inside one datagram.
    private static X509Certificate2 ClientCertificate()
    {
        using var clientKey = RSA.Create(1024);
        var request = new CertificateRequest("CN=client", clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(Now.AddDays(-1), Now.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);
    }

    private static ContentStore Store() => new(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);
}
