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
        Assert.AreEqual("https", session.Head.Scheme);
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
    public async Task ServeAsync_DatagramForNoOpenSession_IsHeldForItsStreamAndNotGivenToAnother()
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
        Assert.IsTrue(log.Notes.Contains("An HTTP datagram for stream 4, which is no open session yet, is held for it."));
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
    public async Task ServeAsync_DatagramsBeforeTheirConnect_AreHeldThenDeliveredInOrderWhenTheSessionOpens()
    {
        using var client = new Http3TestQuicClient();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
            received.SetResult(Convert.ToHexString((await session.ReceiveDatagramAsync(token))!) + Convert.ToHexString((await session.ReceiveDatagramAsync(token))!)));
        var log = await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);

        SendDatagram(client, "00" + "61");
        SendDatagram(client, "00" + "62");
        await client.UntilAsync(() => log.Notes.Count(note => note == "An HTTP datagram for stream 0, which is no open session yet, is held for it.") == 2, Token);
        client.Send(0, UnitTestExtendedConnect.Connect(client));
        var payloads = await received.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.AreEqual("6162", payloads, "ADR-0117 decision 3: held until the session is established, then served in order.");
    }

    [TestMethod]
    public async Task ServeAsync_DatagramPastTheSixtyFourHeld_IsDropped()
    {
        using var client = new Http3TestQuicClient();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<List<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            opened.SetResult();
            var first = new List<byte>();
            for (var index = 0; index < 65; index++)
            {
                first.Add((await session.ReceiveDatagramAsync(token))!.Single());
            }

            received.SetResult(first);
        });
        var log = await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);

        for (var index = 0; index < 65; index++)
        {
            SendDatagram(client, "00" + index.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        await client.UntilAsync(() => log.Notes.Contains("An HTTP datagram for stream 0, which is no open session, was dropped: 64 are held already."), Token);
        client.Send(0, UnitTestExtendedConnect.Connect(client));
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        SendDatagram(client, "00" + "FF");
        var payloads = await received.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        CollectionAssert.AreEqual(Enumerable.Range(0, 64).Select(index => (byte)index).Append((byte)0xFF).ToList(), payloads, "The 65th held datagram (0x40) was dropped; the live one follows the 64.");
    }

    [TestMethod]
    public async Task ServeAsync_HeldDatagramPastTheHeadTimeout_IsDroppedAndTheSessionGetsOnlyLaterOnes()
    {
        using var client = new Http3TestQuicClient();
        var time = UnitTestExtendedConnect.NewClock();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            opened.SetResult();
            received.SetResult(await session.ReceiveDatagramAsync(token));
        });
        var limits = ExchangeLimits.Default with { HeadTimeout = TimeSpan.FromSeconds(5) };
        var log = await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token, limits, time: time);

        SendDatagram(client, "00" + "61");
        await client.UntilAsync(() => log.Notes.Contains("An HTTP datagram for stream 0, which is no open session yet, is held for it."), Token);
        time.Advance(TimeSpan.FromSeconds(5));
        client.Send(0, UnitTestExtendedConnect.Connect(client));
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        SendDatagram(client, "00" + "62");
        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.AreEqual("62", Convert.ToHexString(payload!));
        Assert.IsTrue(log.Notes.Contains("An HTTP datagram held for stream 0 was dropped: no session opened there within the head timeout."));
    }

    [TestMethod]
    public async Task ServeAsync_HeldDatagramWithNoHeadTimeout_IsKeptUntilTheSessionOpens()
    {
        using var client = new Http3TestQuicClient();
        var time = UnitTestExtendedConnect.NewClock();
        var received = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) => received.SetResult(await session.ReceiveDatagramAsync(token)));
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };
        var log = await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token, limits, time: time);

        SendDatagram(client, "00" + "61");
        await client.UntilAsync(() => log.Notes.Contains("An HTTP datagram for stream 0, which is no open session yet, is held for it."), Token);
        time.Advance(TimeSpan.FromSeconds(20));
        client.Send(0, UnitTestExtendedConnect.Connect(client));
        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.AreEqual("61", Convert.ToHexString(payload!));
    }

    [TestMethod]
    public async Task ServeAsync_DatagramForAnEndedSession_IsDroppedAtOnce()
    {
        using var client = new Http3TestQuicClient();
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            await session.RequestStream.WriteHeadersAsync([new(":status", "200")], endStream: true, token);
            ended.SetResult();
        });
        var log = await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token);
        client.Send(0, UnitTestExtendedConnect.Connect(client));
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        await client.UntilAsync(() => client.StreamSent(0).Fin, Token);

        SendDatagram(client, "00" + "61");
        await client.UntilAsync(() => log.Notes.Contains("An HTTP datagram for stream 0, which is no open session, was dropped."), Token);

        Assert.DoesNotContain("An HTTP datagram for stream 0, which is no open session yet, is held for it.", log.Notes, "The session has ended; nothing will open there again.");
    }

    [TestMethod]
    public async Task ServeAsync_HeldDatagramForAStreamThatIsAnOrdinaryRequest_IsDroppedOnceItsHeadIsRead()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        var requestHandler = new UnitTestRequestStreamHandler((stream, token) => stream.WriteHeadersAsync([new(":status", "204")], endStream: true, token));
        var log = await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token, requestHandler: requestHandler);

        SendDatagram(client, "00" + "61");
        await client.UntilAsync(() => log.Notes.Contains("An HTTP datagram for stream 0, which is no open session yet, is held for it."), Token);
        client.Send(0, client.Request("GET", "/"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0) && log.Notes.Contains("1 HTTP datagram(s) held for stream 0 were dropped: it is no session."), Token);

        Assert.AreEqual("204", client.Response(0).Status);
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownWriteOnAnEndedStream_IsLoggedAndTheDrainGoesOn()
    {
        using var shutdown = new CancellationTokenSource();
        using var client = new Http3TestQuicClient();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            session.AddShutdownWrite(_ => throw new InvalidOperationException("the stream has ended"));
            opened.SetResult();
            await WaitUntilCancelledAsync(token);
        });
        var options = new Http3ServerOptions { ExtendedConnectHandlers = [handler], ShutdownGrace = TimeSpan.Zero };
        var log = await UnitTestExtendedConnect.StartAsync(client, options, Token, exchangeToken: shutdown.Token);
        client.Send(0, UnitTestExtendedConnect.Connect(client));
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        await shutdown.CancelAsync();
        await client.FinishAsync(Token);

        Assert.IsTrue(log.Notes.Contains("A shutdown write of session 0 failed: the stream has ended"));
        Assert.EndsWith(GoAwayStream4Hex, ControlStreamHex(client));
        Assert.AreEqual(H3NoError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownWriteThatOutlastsTheGrace_IsCutOffAndGoAwayAndTheCloseFollow()
    {
        using var shutdown = new CancellationTokenSource();
        using var client = new Http3TestQuicClient();
        var time = UnitTestExtendedConnect.NewClock();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            session.AddShutdownWrite(async write =>
            {
                writing.SetResult();
                await Task.Delay(Timeout.Infinite, write);
            });
            opened.SetResult();
            await WaitUntilCancelledAsync(token);
        });
        var options = new Http3ServerOptions { ExtendedConnectHandlers = [handler], ShutdownGrace = TimeSpan.FromSeconds(2) };
        await UnitTestExtendedConnect.StartAsync(client, options, Token, exchangeToken: shutdown.Token, time: time);
        client.Send(0, UnitTestExtendedConnect.Connect(client));
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        await shutdown.CancelAsync();
        await writing.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        var isGoAwayHeldBack = !ControlStreamHex(client).EndsWith(GoAwayStream4Hex, StringComparison.Ordinal);
        time.Advance(TimeSpan.FromSeconds(2));
        await client.FinishAsync(Token);

        Assert.IsTrue(isGoAwayHeldBack, "GOAWAY waits for the shutdown write while the grace lasts.");
        Assert.EndsWith(GoAwayStream4Hex, ControlStreamHex(client));
        Assert.AreEqual(H3NoError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownWithAGrace_RunsTheShutdownWritesThenGoAwayThenServesUntilTheGraceHasPassed()
    {
        using var shutdown = new CancellationTokenSource();
        using var client = new Http3TestQuicClient();
        var time = UnitTestExtendedConnect.NewClock();
        var opened = new TaskCompletionSource<Http3ExtendedConnectSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            await session.RequestStream.WriteHeadersAsync([new(":status", "200")], endStream: false, token);
            Assert.IsTrue(session.AddShutdownWrite(write => session.WriteCapsuleAsync(new Http3Capsule(DrainSessionCapsuleType, []), write)));
            opened.SetResult(session);
            await WaitUntilCancelledAsync(token);
            await session.WriteCapsuleAsync(new Http3Capsule(CloseSessionCapsuleType, [0, 0, 0, 0]), CancellationToken.None);
            await session.RequestStream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true, CancellationToken.None);
        });
        var options = new Http3ServerOptions { ExtendedConnectHandlers = [handler], ShutdownGrace = TimeSpan.FromSeconds(2) };
        var log = await UnitTestExtendedConnect.StartAsync(client, options, Token, exchangeToken: shutdown.Token, time: time);
        client.Send(0, UnitTestExtendedConnect.Connect(client));
        var session = await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        await shutdown.CancelAsync();
        await client.UntilAsync(() => ControlStreamHex(client).EndsWith(GoAwayStream4Hex, StringComparison.Ordinal), Token);
        await session.WriteCapsuleAsync(new Http3Capsule(0x21, [0x63]), Token);
        await client.UntilAsync(() => client.Response(0).Body.Count == 8, Token);
        var servedDuringTheGrace = client.CloseCode is null;
        time.Advance(TimeSpan.FromSeconds(2));
        await client.UntilAsync(() => client.CloseCode is not null, Token);

        Assert.IsTrue(servedDuringTheGrace, "The session is served through the grace.");
        Assert.IsTrue(session.IsShuttingDown);
        Assert.IsFalse(session.AddShutdownWrite(_ => Task.CompletedTask), "Shutdown has begun; the write would never run.");
        Assert.IsLessThan(FrameIndex(client, Http3TestQuicClient.ServerControlStreamId, GoAwayStream4Hex), FrameIndex(client, 0, DrainSessionCapsuleHex), "The shutdown write goes before GOAWAY.");
        Assert.AreEqual(DrainSessionCapsuleHex + "2101" + "63" + "68430400000000", Convert.ToHexString([.. client.Response(0).Body]), "The drain, the write during the grace, then the handler's close once the grace has passed.");
        Assert.IsTrue(client.StreamSent(0).Fin);
        Assert.AreEqual(H3NoError, client.CloseCode);
        Assert.IsTrue(log.Notes.Contains("The exchange was cancelled; sent GOAWAY 4, served the open streams for up to the shutdown grace and closed the HTTP/3 connection with H3_NO_ERROR."));
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownWithAGraceAndNoOpenStream_SendsGoAwayAndClosesWithoutWaitingForTheGrace()
    {
        using var shutdown = new CancellationTokenSource();
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", (_, _) => Task.CompletedTask);
        var options = new Http3ServerOptions { ExtendedConnectHandlers = [handler], ShutdownGrace = TimeSpan.FromHours(1) };
        await UnitTestExtendedConnect.StartAsync(client, options, Token, exchangeToken: shutdown.Token);

        await shutdown.CancelAsync();
        await client.FinishAsync(Token);

        Assert.EndsWith("070100", ControlStreamHex(client), "GOAWAY naming stream 0: no request stream was opened.");
        Assert.AreEqual(H3NoError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_SessionOpeningAfterShutdownBegan_IsShuttingDownAndTakesNoShutdownWrite()
    {
        using var shutdown = new CancellationTokenSource();
        using var client = new Http3TestQuicClient();
        var time = UnitTestExtendedConnect.NewClock();
        var seen = new TaskCompletionSource<(bool IsShuttingDown, bool IsWriteAdded)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            seen.SetResult((session.IsShuttingDown, session.AddShutdownWrite(_ => Task.CompletedTask)));
            await WaitUntilCancelledAsync(token);
        });
        var requestReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestHandler = new UnitTestRequestStreamHandler(async (_, token) =>
        {
            requestReached.SetResult();
            await WaitUntilCancelledAsync(token);
        });
        var options = new Http3ServerOptions { ExtendedConnectHandlers = [handler], ShutdownGrace = TimeSpan.FromSeconds(2) };
        await UnitTestExtendedConnect.StartAsync(client, options, Token, requestHandler: requestHandler, exchangeToken: shutdown.Token, time: time);
        client.Send(0, client.Request("GET", "/slow"), fin: true);
        await requestReached.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        await shutdown.CancelAsync();
        await client.UntilAsync(() => ControlStreamHex(client).EndsWith(GoAwayStream4Hex, StringComparison.Ordinal), Token);
        client.Send(4, UnitTestExtendedConnect.Connect(client));
        var (isShuttingDown, isWriteAdded) = await seen.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        time.Advance(TimeSpan.FromSeconds(2));
        await client.UntilAsync(() => client.CloseCode is not null, Token);

        Assert.IsTrue(isShuttingDown);
        Assert.IsFalse(isWriteAdded);
        Assert.AreEqual(H3NoError, client.CloseCode);
    }

    private const ulong DrainSessionCapsuleType = 0x78ae;

    private const ulong CloseSessionCapsuleType = 0x2843;

    // WT_DRAIN_SESSION (0x78ae, a four-byte integer) with an empty value.
    private const string DrainSessionCapsuleHex = "800078AE" + "00";

    // GOAWAY (0x07), one byte: stream 4, the next after the CONNECT on stream 0.
    private const string GoAwayStream4Hex = "070104";

    private static void SendDatagram(Http3TestQuicClient client, string hex) =>
        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicDatagramFrame(Convert.FromHexString(hex))));

    private static string ControlStreamHex(Http3TestQuicClient client) =>
        Convert.ToHexString(client.StreamSent(Http3TestQuicClient.ServerControlStreamId).Data);

    private static int FrameIndex(Http3TestQuicClient client, ulong streamId, string hex) =>
        client.Quic.FramesReceived[QuicPacketType.OneRtt].FindIndex(frame =>
            frame is QuicStreamFrame stream && stream.StreamId == streamId && Convert.ToHexString(stream.Data.Span).Contains(hex, StringComparison.Ordinal));

    private static async Task WaitUntilCancelledAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The grace has passed.
        }
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
