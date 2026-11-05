using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// Extended CONNECT, HTTP datagrams and claimed stream types on an <see cref="Http3ConnectionServer"/>
/// (BL-411, ADR-0086 decision 3), as <see cref="Http3ExtendedConnect"/> routes them. Every byte the
/// client sends is written from RFC 9220 section 3 (extended CONNECT, setting 0x08), RFC 9297
/// sections 2.1 and 3.2 (the quarter stream ID, setting and error 0x33, capsules) and
/// draft-ietf-webtrans-http3-16 section 4.3 (signal 0x41 and stream type 0x54, each followed by
/// the session ID), after pinned upstream curl's recorded Initials.
/// </summary>
[TestClass]
public sealed class Http3ExtendedConnectTests
{
    private const ulong H3NoError = 0x100;
    private const ulong H3StreamCreationError = 0x103;
    private const ulong H3FrameError = 0x106;
    private const ulong H3IdError = 0x108;
    private const ulong H3MessageError = 0x10e;
    private const ulong H3DatagramError = 0x33;

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task ServeAsync_HandlerRegistered_SendsSettingsWithEnableConnectProtocolAndH3DatagramThenFurtherSettings()
    {
        using var client = new Http3TestQuicClient();
        var options = new Http3ServerOptions
        {
            ExtendedConnectHandlers = [new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask)],
            FurtherSettings = [new(0x2c7cf000, 1)],
        };

        await UnitTestExtendedConnect.StartAsync(client, options, Token);
        await client.UntilAsync(() => client.StreamSent(Http3TestQuicClient.ServerControlStreamId).Data.Length >= 17, Token);

        // Control stream type 0x00; SETTINGS (0x04), 14 bytes: MAX_FIELD_SECTION_SIZE (0x06) 1048576,
        // ENABLE_CONNECT_PROTOCOL (0x08) 1, H3_DATAGRAM (0x33) 1, then 0x2c7cf000 1.
        Assert.AreEqual(
            "00" + "040E" + "0680100000" + "0801" + "3301" + "AC7CF00001",
            Convert.ToHexString(client.StreamSent(Http3TestQuicClient.ServerControlStreamId).Data));
    }

    [TestMethod]
    public async Task ServeAsync_NoHandlerAndNoMessageLimit_SendsAnEmptySettings()
    {
        using var client = new Http3TestQuicClient();

        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions(), Token, ExchangeLimits.Default with { MaxMessageBytes = 0 });
        await client.UntilAsync(() => client.StreamSent(Http3TestQuicClient.ServerControlStreamId).Data.Length >= 3, Token);

        Assert.AreEqual("000400", Convert.ToHexString(client.StreamSent(Http3TestQuicClient.ServerControlStreamId).Data));
    }

    [TestMethod]
    public async Task ServeAsync_ExtendedConnectForARegisteredProtocol_ReachesItsHandler()
    {
        using var client = new Http3TestQuicClient();
        var reached = new TaskCompletionSource<Http3ExtendedConnectSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            await session.RequestStream.WriteHeadersAsync([new(":status", "200")], endStream: false, token);
            reached.SetResult(session);
            await Task.Delay(Timeout.Infinite, token);
        });
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);

        client.Send(0, UnitTestExtendedConnect.Connect(client));
        var session = await reached.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        await client.UntilAsync(() => client.StreamSent(0).Data.Length > 0, Token);

        Assert.AreEqual(0UL, session.Id);
        Assert.AreEqual("CONNECT", session.Head.Method);
        Assert.AreEqual("webtransport-h3", session.Head.Protocol);
        Assert.AreEqual("/echo", session.Head.Target);
        Assert.AreEqual("127.0.0.1:18050", session.Head.Authority);
        Assert.AreEqual("200", client.Response(0).Status);
        Assert.IsFalse(client.Response(0).Fin, "A 2xx accepts the session, whose stream stays open.");
        Assert.IsNull(client.StopSendingCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_ExtendedConnectForAnUnregisteredProtocol_Answers501WithFinThenStopsWithNoError()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => throw new AssertFailedException("The session must not reach the handler."));
        var log = await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler], ServerFieldValue = "surl" }, Token);

        client.Send(0, UnitTestExtendedConnect.Connect(client, "websocket"));
        await client.UntilAsync(() => client.StopSendingCode(0) is not null, Token);

        var refused = client.Response(0);
        Assert.AreEqual("501", refused.Status, "RFC 9220 section 3: SHOULD respond with a 501 (Not Implemented).");
        CollectionAssert.AreEqual(new[] { "server: surl", "content-length: 0" }, refused.RegularFields);
        Assert.IsTrue(refused.Fin);
        Assert.AreEqual(H3NoError, client.StopSendingCode(0));
        Assert.IsNull(client.ResetCode(0));
        Assert.IsTrue(log.Notes.Contains("Stream 0 was answered 501: no handler serves the extended CONNECT protocol websocket."));
    }

    [TestMethod]
    public async Task ServeAsync_ProtocolWithNoHandlerRegistered_ResetsTheStreamWithMessageError()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions(), Token);

        client.Send(0, UnitTestExtendedConnect.Connect(client));
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(H3MessageError, client.ResetCode(0), "Without SETTINGS_ENABLE_CONNECT_PROTOCOL, :protocol is an unknown pseudo-header.");
    }

    [TestMethod]
    public async Task ServeAsync_ExtendedConnectWithoutPath_ResetsTheStreamWithMessageError()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => throw new AssertFailedException("The session must not reach the handler."));
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);

        client.Send(0, client.Headers((":method", "CONNECT"), (":protocol", "webtransport-h3"), (":scheme", "https"), (":authority", "127.0.0.1:18050")));
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(H3MessageError, client.ResetCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_DatagramForNoOpenSession_IsDroppedAndTheNextOneIsDelivered()
    {
        using var client = new Http3TestQuicClient();
        var received = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            opened.SetResult();
            received.SetResult(await session.ReceiveDatagramAsync(token));
        });
        var log = await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);
        client.Send(0, UnitTestExtendedConnect.Connect(client));
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicDatagramFrame(Convert.FromHexString("01" + "78"))));
        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicDatagramFrame(Convert.FromHexString("00" + "79"))));
        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.AreEqual("79", Convert.ToHexString(payload!), "Quarter stream ID 0 names stream 0; ID 1 names stream 4, no session.");
        Assert.IsTrue(log.Notes.Contains("An HTTP datagram for stream 4, which is no open session, was dropped."));
    }

    [TestMethod]
    [DataRow("", DisplayName = "No quarter stream ID at all")]
    [DataRow("D000000000000000", DisplayName = "Quarter stream ID 2^60")]
    public async Task ServeAsync_DatagramWithABadQuarterStreamId_ClosesTheConnectionWithH3DatagramError(string datagramHex)
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);

        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicDatagramFrame(Convert.FromHexString(datagramHex))));
        await client.UntilAsync(() => client.CloseCode is not null, Token);

        Assert.AreEqual(H3DatagramError, client.CloseCode, "RFC 9297 section 2.1: H3_DATAGRAM_ERROR (0x33).");
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesTheConnection_EndsTheDatagramReaderAndTheServer()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);

        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicConnectionCloseFrame(true, H3NoError, 0, ReadOnlyMemory<byte>.Empty)));
        await client.FinishAsync(Token);

        Assert.IsTrue(client.Serving!.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task ServeAsync_SignalAfterTheFirstFrameOfARequestStream_ClosesTheConnectionWithFrameError()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);

        // A reserved frame type 0x21 with no payload, then the signal 0x41 and session ID 0.
        client.Send(0, Convert.FromHexString("2100" + "4041" + "00"));
        await client.UntilAsync(() => client.CloseCode is not null, Token);

        Assert.AreEqual(H3FrameError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_SignalAsAFrameAfterTheHeaders_ClosesTheConnectionWithFrameError()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        var requestHandler = new UnitTestRequestStreamHandler(async (stream, token) =>
        {
            while (await stream.ReadBodyAsync(token) is not null)
            {
            }
        });
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token, requestHandler: requestHandler);

        client.Send(0, [.. client.Request("POST", "/"), .. Convert.FromHexString("4041" + "00")]);
        await client.UntilAsync(() => client.CloseCode is not null, Token);

        Assert.AreEqual(H3FrameError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ReservedFrameAfterTheHeadersWithAHandlerRegistered_IsSkippedAsBefore()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        var body = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestHandler = new UnitTestRequestStreamHandler(async (stream, token) =>
        {
            var bytes = new List<byte>();
            while (await stream.ReadBodyAsync(token) is { } piece)
            {
                bytes.AddRange(piece);
            }

            body.SetResult(Convert.ToHexString([.. bytes]));
        });
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token, requestHandler: requestHandler);

        client.Send(0, [.. client.Request("POST", "/"), .. Convert.FromHexString("2100"), .. Http3TestQuicClient.Frame(Http3FrameType.Data, [0x68, 0x69])], fin: true);

        Assert.AreEqual("6869", await body.Task.WaitAsync(TimeSpan.FromSeconds(30), Token));
    }

    [TestMethod]
    public async Task ServeAsync_StreamNamingNoOpenSession_GoesToTheHandlersServeStreamWithoutSession()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);

        client.Send(0, Convert.FromHexString("4041" + "04"));
        var stream = await handler.StreamWithoutSession.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.AreEqual(0UL, stream.Id);
        Assert.AreEqual(4UL, stream.SessionId);
        Assert.IsTrue(stream.Stream.IsBidirectional);
    }

    [TestMethod]
    public async Task ServeAsync_StreamNamingAnotherHandlersSession_GoesToItsOwnHandlerWithoutSession()
    {
        using var client = new Http3TestQuicClient();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var other = new UnitTestExtendedConnectHandler("other", async (_, token) =>
        {
            opened.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        })
        {
            BidirectionalStreamSignal = 0x42,
            UnidirectionalStreamType = null,
        };
        var webTransport = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [other, webTransport] }, Token);
        client.Send(0, UnitTestExtendedConnect.Connect(client, "other"));
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        client.Send(4, Convert.FromHexString("4041" + "00"));
        var stream = await webTransport.StreamWithoutSession.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.AreEqual(4UL, stream.Id);
        Assert.AreEqual(0UL, stream.SessionId);
    }

    [TestMethod]
    public async Task ServeAsync_SessionIdThatIsNotAClientBidirectionalStream_ClosesTheConnectionWithIdError()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);

        client.Send(0, Convert.FromHexString("4041" + "05"));
        await client.UntilAsync(() => client.CloseCode is not null, Token);

        Assert.AreEqual(H3IdError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_UnidirectionalStreamOfAnUnregisteredType_IsStoppedWithStreamCreationError()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token, openQpackStreams: false);

        client.Send(Http3TestQuicClient.EncoderStreamId, Convert.FromHexString("21"));
        await client.UntilAsync(() => client.StopSendingCode(Http3TestQuicClient.EncoderStreamId) is not null, Token);

        Assert.AreEqual(H3StreamCreationError, client.StopSendingCode(Http3TestQuicClient.EncoderStreamId));
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public void Constructor_TwoHandlersForOneProtocol_ThrowsArgument()
    {
        var options = new Http3ServerOptions
        {
            ExtendedConnectHandlers =
            [
                new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask),
                new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask),
            ],
        };
        var context = new ExchangeContext(
            1,
            new ListenUrl("https", "127.0.0.1", 18050).WithBoundPort(18050),
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 18050),
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            TimeProvider.System,
            CancellationToken.None);

        Assert.ThrowsExactly<ArgumentException>(() => new Http3ConnectionServer(new UnitTestRequestStreamHandler((_, _) => Task.CompletedTask), options, context));
    }
}
