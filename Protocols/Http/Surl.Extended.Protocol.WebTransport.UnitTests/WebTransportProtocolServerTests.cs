using System.Net;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>The server <c>surl</c> registers for <c>webtransport</c> (BL-461): its scheme, its arguments, the certificate it waits for and the connections it serves.</summary>
[TestClass]
public sealed class WebTransportProtocolServerTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public void Schemes_IsWebTransportOnly()
    {
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero);

        CollectionAssert.AreEqual(new[] { "webtransport" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ConstructorAndMethods_NullArguments_Throw()
    {
        var policy = new AnonymousAuthenticationPolicy();
        var server = new WebTransportProtocolServer(policy, () => null, TimeSpan.Zero);
        using var client = new Http3TestQuicClient();

        Assert.ThrowsExactly<ArgumentNullException>(() => new WebTransportProtocolServer(null!, () => null, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WebTransportProtocolServer(policy, null!, TimeSpan.Zero));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, NewContext(new ManualTimeProvider(Now), Token)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(client.Flow, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, Token).AsTask());
    }

    [TestMethod]
    public async Task ServeAsync_NoCertificateYet_ReturnsAtOnceAndSendsNothing()
    {
        using var client = new Http3TestQuicClient();
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero);

        await server.ServeAsync(client.Flow, NewContext(new ManualTimeProvider(Now), Token)).WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.IsEmpty(client.Flow.SentDatagrams);
    }

    [TestMethod]
    public async Task ServeAsync_CertificateServed_AcceptsAWebTransportSessionOnAnyPath()
    {
        using var client = new Http3TestQuicClient();
        var serving = StartServing(client, NewContext(new ManualTimeProvider(Now), Token), TimeSpan.FromSeconds(30));
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/any/path");

        Assert.AreEqual("200", response.Status);
        Assert.IsFalse(serving.IsCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ClientsFourthUnidirectionalStream_IsTakenAndAnswered()
    {
        using var client = new Http3TestQuicClient();
        var serving = StartServing(client, NewContext(new ManualTimeProvider(Now), Token), TimeSpan.FromSeconds(30));
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(14, Convert.FromHexString(UnitTestWebTransport.UnidirectionalHeaderHex + "64617461"), fin: true);
        await client.UntilAsync(() => client.StreamSent(UnitTestWebTransport.ServerAnswerStreamId).Fin, Token);

        Assert.AreEqual(UnitTestWebTransport.UnidirectionalHeaderHex + "64617461", UnitTestWebTransport.SentHex(client, UnitTestWebTransport.ServerAnswerStreamId), "The control and two QPACK streams are the client's first three; a session's stream is its fourth.");
        Assert.IsFalse(serving.IsCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_SessionStreamPayload_IsReportedToTheDecryptedBytesSink()
    {
        var sink = new UnitTestRecordingDecryptedBytesSink();
        using var client = new Http3TestQuicClient();
        var context = NewContext(new ManualTimeProvider(Now), Token) with { DecryptedBytes = sink };
        var serving = StartServing(client, context, TimeSpan.FromSeconds(30));
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(14, Convert.FromHexString(UnitTestWebTransport.UnidirectionalHeaderHex + "64617461"), fin: true);
        await client.UntilAsync(() => client.StreamSent(UnitTestWebTransport.ServerAnswerStreamId).Fin, Token);

        Assert.IsTrue(sink.HasReceived("data"));
        Assert.IsFalse(serving.IsCompleted);
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "No head timeout")]
    [DataRow(0, DisplayName = "A head timeout longer than a timer can wait")]
    [DataRow(30, DisplayName = "A head timeout")]
    public async Task ServeAsync_AnyHeadTimeout_StillCompletesTheHandshake(int seconds)
    {
        using var client = new Http3TestQuicClient();
        var headTimeout = seconds switch
        {
            -1 => Timeout.InfiniteTimeSpan,
            0 => TimeSpan.FromDays(60),
            _ => TimeSpan.FromSeconds(seconds),
        };
        var context = NewContext(new ManualTimeProvider(Now), Token) with { Limits = ExchangeLimits.Default with { HeadTimeout = headTimeout } };
        _ = StartServing(client, context, TimeSpan.Zero);
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token);

        Assert.AreEqual("200", response.Status);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeTheHandshakeCompletes_StopsTheConnection()
    {
        using var client = new Http3TestQuicClient();
        using var exchange = new CancellationTokenSource();
        var serving = StartServing(client, NewContext(new ManualTimeProvider(Now), exchange.Token), TimeSpan.Zero);

        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), Token));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileServing_LeavesTheEndingToTheHttp3Server()
    {
        using var client = new Http3TestQuicClient();
        using var exchange = new CancellationTokenSource();
        var time = new ManualTimeProvider(Now);
        var serving = StartServing(client, NewContext(time, exchange.Token), TimeSpan.Zero);
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        await exchange.CancelAsync();
        await UnitTestWebTransport.AdvanceUntilAsync(client, time, () => client.CloseCode is not null, Token);
        time.Advance(TimeSpan.FromMinutes(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.AreEqual(0x100UL, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_FlowCutByAGracefulDropFault_ClosesTheConnectionWithNoErrorInPlaceOfTheEcho()
    {
        using var client = new Http3TestQuicClient();
        var certificate = new WebTransportServerCertificate([client.SelfSignedCertificate()], client.Key);
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => certificate, TimeSpan.Zero);
        var cut = new UnitTestGracefullyCutDatagramFlow(client.Flow, long.MaxValue);
        _ = server.ServeAsync(cut, NewContext(new ManualTimeProvider(Now), Token));
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        cut.BytesLeft = 0;
        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "64617461"), fin: true);
        await client.UntilAsync(() => client.CloseCode is not null, Token);

        var closes = client.OneRttFrames<QuicConnectionCloseFrame>().Select(frame => (frame.IsApplicationError, frame.ErrorCode)).Distinct();
        Assert.AreEqual((false, (ulong)QuicTransportErrorCode.NoError), closes.Single());
        Assert.IsEmpty(client.StreamSent(4).Data);
        Assert.IsTrue(cut.IsCut);
        Assert.IsNotEmpty(cut.ClosingDatagrams);
    }

    [TestMethod]
    public void Server_HasAGracefulCloseOfItsOwnForACutFlow()
    {
        IGracefulCutDatagramProtocolServer server = Assert.IsInstanceOfType<IGracefulCutDatagramProtocolServer>(
            new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero));

        Assert.IsTrue(server.ClosesCutFlowsGracefully(new ListenUrl("webtransport", "127.0.0.1", 18050)));
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_AVersion1Initial_SendsOneInitialPacket(ConnectionRefusal refusal)
    {
        using var client = new Http3TestQuicClient();
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero);

        await server.WriteRefusalAsync(client.Flow, refusal, Token);

        Assert.HasCount(1, client.Flow.SentDatagrams);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_FirstDatagramNotAVersion1Initial_SendsNoBytes()
    {
        using var client = new Http3TestQuicClient();
        var first = client.Quic.InitialDatagrams[0].ToArray();
        first[4] = 0x02;
        await using var flow = new InMemoryDatagramFlow(first);
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero);

        await server.WriteRefusalAsync(flow, ConnectionRefusal.TooManyConnections, Token);

        Assert.IsEmpty(flow.SentDatagrams);
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static ExchangeContext NewContext(TimeProvider time, CancellationToken token) => new(
        1,
        new ListenUrl("webtransport", "127.0.0.1", 18050).WithBoundPort(18050),
        new IPEndPoint(IPAddress.Loopback, 18050),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        time,
        token);

    private static Task StartServing(Http3TestQuicClient client, ExchangeContext context, TimeSpan idleTimeout)
    {
        var certificate = new WebTransportServerCertificate([client.SelfSignedCertificate()], client.Key);
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => certificate, idleTimeout);
        return server.ServeAsync(client.Flow, context);
    }

    [TestMethod]
    public void CreateRedactor_DecodedAuthorizationLine_IsLoggedAsStars()
    {
        var logged = System.Text.Encoding.ASCII.GetString(
            RedactDecodedText(System.Text.Encoding.ASCII.GetBytes(":method: CONNECT\r\nauthorization: Bearer sekret-token\r\n")));

        Assert.DoesNotContain("sekret", logged);
        Assert.Contains("authorization: ****", logged);
    }

    [TestMethod]
    public void CreateRedactor_EachApiKeyAndSessionTokenHeadersFrame_IsLoggedWithItsPayloadAsStars()
    {
        using var client = new Http3TestQuicClient();
        foreach (var name in HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
        {
            var frame = client.Headers((":method", "CONNECT"), (name.ToLowerInvariant(), "sekret-key"));

            var logged = Redact(frame);

            // The frame type and its one-byte length are logged as received.
            byte[] expected = [.. frame[..2], .. "****"u8];
            CollectionAssert.AreEqual(expected, logged, name);
        }
    }

    [TestMethod]
    public void CreateRedactor_EachDecodedApiKeyAndSessionTokenLine_IsLoggedAsStars()
    {
        foreach (var name in HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
        {
            var lowerName = name.ToLowerInvariant();
            var text = $":method: CONNECT\r\n{lowerName}: sekret-key\r\nx-trace: abc\r\n\r\n";

            var logged = RedactDecodedText(System.Text.Encoding.ASCII.GetBytes(text));

            Assert.AreEqual(
                $":method: CONNECT\r\n{lowerName}: ****\r\nx-trace: abc\r\n\r\n",
                System.Text.Encoding.ASCII.GetString(logged),
                name);
        }
    }

    [TestMethod]
    public async Task ServeAsync_SessionStreamSentInSeveralChunks_IsLoggedByTheServersRedactorAsReceived()
    {
        using var client = new Http3TestQuicClient();
        var certificate = new WebTransportServerCertificate([client.SelfSignedCertificate()], client.Key);
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => certificate, TimeSpan.FromSeconds(30));
        var sink = new UnitTestRedactingDecryptedBytesSink(NewStreamRedactor());
        var context = NewContext(new ManualTimeProvider(Now), Token) with { DecryptedBytes = sink };
        var serving = server.ServeAsync(client.Flow, context);
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        // The signal 0x41's two-byte integer split after its first byte, the second arriving with the session
        // ID, then data that reads as an HTTP/3 DATA frame and a HEADERS frame that does not decode.
        client.Send(4, Convert.FromHexString("40"));
        client.Send(4, Convert.FromHexString("4100"));
        client.Send(4, Convert.FromHexString("000161"));
        await client.UntilAsync(() => UnitTestWebTransport.SentHex(client, 4) == "000161", Token);
        client.Send(4, Convert.FromHexString("0103FFFFFF"), fin: true);
        await client.UntilAsync(() => client.StreamSent(4).Fin, Token);

        var chunks = sink.LoggedChunks(4);
        Assert.AreEqual("0001610103FFFFFF", UnitTestWebTransport.SentHex(client, 4));
        Assert.IsGreaterThan(1, chunks.Length);
        Assert.AreEqual("4041000001610103FFFFFF", Convert.ToHexString([.. chunks.SelectMany(chunk => chunk)]));
        Assert.IsFalse(serving.IsCompleted);
    }

    [TestMethod]
    [DataRow(4L, "404100", DisplayName = "A bidirectional stream, signal 0x41")]
    [DataRow(14L, "405400", DisplayName = "A unidirectional stream, type 0x54")]
    public void CreateRedactor_ASessionStreamsDataShapedLikeASecretHeadersFrame_IsLoggedAsReceived(long streamId, string headerHex)
    {
        using var client = new Http3TestQuicClient();
        var redactor = NewStreamRedactor();
        byte[] stream = [.. Convert.FromHexString(headerHex), .. client.Headers(("authorization", "Bearer session-data"))];

        var logged = RedactStream(redactor, streamId, stream);

        CollectionAssert.AreEqual(stream, logged);
    }

    [TestMethod]
    public void CreateRedactor_MoreSessionStreamsThanItRemembers_StillLogsALaterRequestsFramesAsReceived()
    {
        using var client = new Http3TestQuicClient();
        var redactor = NewStreamRedactor();
        var sessionStream = Convert.FromHexString("404100000561");
        const int SessionStreams = 4097;
        for (var stream = 0; stream < SessionStreams; stream++)
        {
            CollectionAssert.AreEqual(sessionStream, RedactStream(redactor, stream * 4L, sessionStream), $"stream {stream}");
        }

        var headers = client.Headers((":method", "CONNECT"), (":path", "/echo"), ("x-trace", "abc"));
        var data = UnitTestWebTransport.Data("68656C6C6F");

        var loggedHeaders = RedactStream(redactor, SessionStreams * 4L, headers);
        var loggedData = RedactStream(redactor, SessionStreams * 4L, data);

        CollectionAssert.AreEqual(headers, loggedHeaders);
        CollectionAssert.AreEqual(data, loggedData);
    }

    private static IStreamReceivedBytesRedactor NewStreamRedactor() => Assert.IsInstanceOfType<IStreamReceivedBytesRedactor>(
        new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero).CreateRedactor(new ExchangeRedactionContext(1, "webtransport")));

    private static byte[] RedactStream(IStreamReceivedBytesRedactor redactor, long streamId, byte[] received)
    {
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        redactor.RedactStream(streamId, received, output);

        return output.WrittenSpan.ToArray();
    }

    private static byte[] RedactDecodedText(byte[] decodedText)
    {
        var redactor = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero).CreateRedactor(new ExchangeRedactionContext(1, "webtransport"));
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        Assert.IsInstanceOfType<IDecodedTextReceivedBytesRedactor>(redactor).RedactDecodedText(decodedText, output);

        return output.WrittenSpan.ToArray();
    }

    private static byte[] Redact(byte[] received)
    {
        var redactor = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero).CreateRedactor(new ExchangeRedactionContext(1, "webtransport"));
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        redactor.Redact(received, output);
        redactor.Flush(output);

        return output.WrittenSpan.ToArray();
    }
}
