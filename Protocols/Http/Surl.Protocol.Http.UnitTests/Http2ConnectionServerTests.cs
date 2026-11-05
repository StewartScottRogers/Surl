using Surl.Content;
using Surl.Hpack;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.Http2TestClient;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2ConnectionServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_RecordedPriorKnowledgeGetOfAFile_AnswersWhatHttp11AnswersForTheSameFile()
    {
        var request = RecordedFixture.ReadRequestBytes("h2-prior-knowledge-get-file");

        var (connection, log) = await ServeAsync(request);

        var written = connection.WrittenBytes;
        CollectionAssert.AreEqual(DefaultServerSettings, written[..DefaultServerSettings.Length], "The server's SETTINGS comes first.");
        Assert.IsTrue(Frames(written)[1] is Http2SettingsFrame { Ack: true }, "Then the ACK of curl's SETTINGS.");
        var response = Responses(written)[1];
        var (http11Fields, http11Body) = Http11Answer(RecordedResponse("get-file"));
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(http11Fields, RegularFields(response));
        Assert.AreEqual(http11Body, response.BodyText);
        Assert.IsTrue(response.EndedOnData);
        Assert.IsNull(GoAway(written), "The client closed; the server sends no GOAWAY of its own.");
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToArray(), "The client sent GOAWAY NoError, last stream 0.");
    }

    [TestMethod]
    public async Task ServeAsync_TwoConcurrentStreams_AnswersEachIndependently()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, endStream: false), client.Get(3));
        var (connection, exchange, serving) = StartServing(request);

        await WaitUntilAsync(() => HasEnded(connection, 3));
        var responses = Responses(connection.WrittenBytes);
        await exchange.CancelAsync();
        await serving;

        Assert.AreEqual("200", responses[3].Status);
        Assert.AreEqual(FileBody, responses[3].BodyText);
        Assert.IsFalse(responses.ContainsKey(1), "Stream 1 still waits for its body.");
        var goAway = GoAway(connection.WrittenBytes)!;
        Assert.AreEqual(3, goAway.LastStreamId);
        Assert.AreEqual(Http2ErrorCode.NoError, goAway.ErrorCode);
    }

    [TestMethod]
    public async Task ServeAsync_BodyLargerThanTheClientsWindow_SendsOnlyWhatWindowUpdatesAllow()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Start(new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 5)),
            client.Get(1),
            Frame(new Http2WindowUpdateFrame(1, 5)));

        var (connection, log) = await ServeAsync(request);

        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual(FileBody[..10], response.BodyText);
        Assert.IsFalse(response.EndedOnData);
        CollectionAssert.Contains(log.Notes.ToArray(), "Stream 1 ended before its answer was sent: The client closed the connection before granting stream 1 the window its DATA needs.");
    }

    [TestMethod]
    public async Task ServeAsync_WindowUpdatesCoveringTheBody_SendsItWholeWithEndStream()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Start(new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 5)),
            client.Get(1),
            Frame(new Http2WindowUpdateFrame(1, 5)),
            Frame(new Http2WindowUpdateFrame(1, 7)));

        var (connection, _) = await ServeAsync(request);

        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual(FileBody, response.BodyText);
        Assert.IsTrue(response.EndedOnData);
        Assert.AreEqual(5, response.DataFrameLengths[0], "The first DATA fills the initial window and no more.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientResetsAStreamWaitingForWindow_EndsItsAnswerQuietly()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Start(new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 0)),
            client.Get(1),
            Frame(new Http2RstStreamFrame(1, Http2ErrorCode.Cancel)));

        var (connection, log) = await ServeAsync(request);

        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual("200", response.Status);
        Assert.IsEmpty(response.Body);
        Assert.IsNull(response.ResetCode);
        Assert.IsFalse(log.Notes.Any(note => note.StartsWith("Stream 1 ended", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_RequestOverTheConcurrentStreamLimit_IsRefusedAndTheOthersAreNot()
    {
        var client = new Http2TestClient();
        var opening = Enumerable.Range(0, Http2ConnectionServer.MaxConcurrentStreams).Select(index => client.Get((2 * index) + 1, endStream: false));
        var request = Concat([Start(), .. opening, client.Get(201)]);

        var (connection, _) = await ServeAsync(request);

        var responses = Responses(connection.WrittenBytes);
        Assert.AreEqual(Http2ErrorCode.RefusedStream, responses[201].ResetCode);
        Assert.IsFalse(responses.Where(response => response.Key < 201).Any(response => response.Value.ResetCode == Http2ErrorCode.RefusedStream));
    }

    [TestMethod]
    public async Task ServeAsync_HeaderBlockOverTheMessageLimit_EndsWithGoAwayEnhanceYourCalm()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, fields: ("x-long", new string('a', 100))));

        var (connection, log) = await ServeAsync(request, ExchangeLimits.Default with { MaxMessageBytes = 64 });

        AssertGoAway(connection, 1, Http2ErrorCode.EnhanceYourCalm);
        Assert.IsFalse(Responses(connection.WrittenBytes).ContainsKey(1));
        CollectionAssert.Contains(log.Notes.ToArray(), "the header block of stream 1 is longer than the 64-byte message limit; answered GOAWAY EnhanceYourCalm and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_HeaderListOverTheMessageLimit_EndsWithGoAwayEnhanceYourCalm()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, fields: [.. Enumerable.Repeat(("accept", "*/*"), 5)]));

        var (connection, _) = await ServeAsync(request, ExchangeLimits.Default with { MaxMessageBytes = 200 });

        AssertGoAway(connection, 1, Http2ErrorCode.EnhanceYourCalm);
    }

    [TestMethod]
    public async Task ServeAsync_HeaderBlockThatDoesNotDecode_EndsWithGoAwayCompressionError()
    {
        var request = Concat(Start(), Frame(new Http2HeadersFrame(1, [0x80], EndStream: true, EndHeaders: true)));

        var (connection, _) = await ServeAsync(request);

        AssertGoAway(connection, 1, Http2ErrorCode.CompressionError);
    }

    [TestMethod]
    public async Task ServeAsync_NoMessageLimit_LeavesMaxHeaderListSizeOutOfItsSettings()
    {
        var (connection, _) = await ServeAsync(Start(), ExchangeLimits.Default with { MaxMessageBytes = 0 });

        var settings = (Http2SettingsFrame)Frames(connection.WrittenBytes)[0];
        Assert.AreEqual(new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 100), settings.Settings.Single());
    }

    [TestMethod]
    [DataRow("missing :path")]
    [DataRow("uppercase field name")]
    public async Task ServeAsync_MalformedRequest_IsResetProtocolErrorAndTheConnectionCarriesOn(string malformation)
    {
        var client = new Http2TestClient();
        HpackHeaderField[] fields = malformation == "missing :path"
            ? [new(":method", "GET"), new(":scheme", "http"), new(":authority", "127.0.0.1")]
            : [.. RequestFields("GET", "/file.txt"), new("User-Agent", "curl/8.21.0")];
        var request = Concat(Start(), client.Headers(1, fields, endStream: true), client.Get(3));

        var (connection, _) = await ServeAsync(request);

        var responses = Responses(connection.WrittenBytes);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, responses[1].ResetCode);
        Assert.AreEqual("200", responses[3].Status);
    }

    [TestMethod]
    public async Task ServeAsync_Ping_IsAnsweredWithAnAckOfTheSameBytes()
    {
        var request = Concat(Start(), Frame(new Http2PingFrame(false, 0x0102030405060708)), Frame(new Http2PingFrame(true, 9)));

        var (connection, _) = await ServeAsync(request);

        byte[] expected = [0x00, 0x00, 0x08, 0x06, 0x01, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        CollectionAssert.AreEqual(expected, Frame(Frames(connection.WrittenBytes).OfType<Http2PingFrame>().Single()));
    }

    [TestMethod]
    public async Task ServeAsync_Shutdown_SendsGoAwayNamingTheLastStreamAndLetsTheCancellationEscape()
    {
        var client = new Http2TestClient();
        var connection = new InMemoryConnection([Concat(Start(), client.Get(1))], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var context = Context(log, new ManualTimeProvider(Now), shutdown.Token) with { ShutdownToken = shutdown.Token };
        var serving = Server().ServeAsync(connection, context);

        await WaitUntilAsync(() => HasEnded(connection, 1));
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        AssertGoAway(connection, 1, Http2ErrorCode.NoError);
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToArray(), "The exchange was cancelled; answered GOAWAY NoError, last stream 1, and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_FarewellWriteStalls_GivesUpAtItsDeadline()
    {
        var connection = new StalledWriteConnection(Preface);
        var log = new RecordingExchangeLog();
        var clock = new ManualTimeProvider(Now);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server().ServeAsync(connection, Context(log, clock, exchange.Token));

        await exchange.CancelAsync();
        await WaitForTimersAsync(clock, 1);
        clock.Advance(Http2ConnectionServer.FarewellWriteDeadline);
        await serving;

        CollectionAssert.Contains(log.Notes.ToArray(), "The farewell GOAWAY was not written within its 1-second write deadline; the connection is closed without it.");
    }

    [TestMethod]
    public async Task ServeAsync_FirstFrameNotSettings_EndsWithGoAwayProtocolError()
    {
        var request = Concat(Preface, Frame(new Http2PingFrame(false, 1)));

        var (connection, _) = await ServeAsync(request);

        AssertGoAway(connection, 0, Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public async Task ServeAsync_FrameInterruptingAHeaderBlock_EndsWithGoAwayProtocolError()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Start(),
            Frame(new Http2HeadersFrame(1, client.Block(RequestFields("GET", "/file.txt")), EndStream: true, EndHeaders: false)),
            Frame(new Http2PingFrame(false, 1)));

        var (connection, _) = await ServeAsync(request);

        AssertGoAway(connection, 1, Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public async Task ServeAsync_ContinuationOfAnotherStream_EndsWithGoAwayProtocolError()
    {
        var block = new Http2TestClient().Block(RequestFields("GET", "/file.txt"));
        var request = Concat(
            Start(),
            Frame(new Http2HeadersFrame(1, block[..3], EndStream: true, EndHeaders: false)),
            Frame(new Http2ContinuationFrame(3, block[3..], EndHeaders: true)));

        var (connection, _) = await ServeAsync(request);

        AssertGoAway(connection, 1, Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public async Task ServeAsync_HeaderBlockEndedByContinuation_IsAnswered()
    {
        var block = new Http2TestClient().Block(RequestFields("GET", "/file.txt"));
        var request = Concat(
            Start(),
            Frame(new Http2HeadersFrame(1, block[..3], EndStream: true, EndHeaders: false)),
            Frame(new Http2ContinuationFrame(1, block[3..], EndHeaders: true)));

        var (connection, _) = await ServeAsync(request);

        Assert.AreEqual(FileBody, Responses(connection.WrittenBytes)[1].BodyText);
    }

    [TestMethod]
    public async Task ServeAsync_ContinuationWithNoHeaderBlock_EndsWithGoAwayProtocolError()
    {
        var request = Concat(Start(), Frame(new Http2ContinuationFrame(1, [0x82], EndHeaders: true)));

        var (connection, _) = await ServeAsync(request);

        AssertGoAway(connection, 0, Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public async Task ServeAsync_UnknownFrameTypeAndPriority_AreIgnored()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Start(),
            [0x00, 0x00, 0x01, 0xFA, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF],
            Frame(new Http2PriorityFrame(1, new Http2Priority(false, 0, 16))),
            client.Get(1));

        var (connection, _) = await ServeAsync(request);

        Assert.AreEqual("200", Responses(connection.WrittenBytes)[1].Status);
    }

    [TestMethod]
    public async Task ServeAsync_MalformedConnectionFrame_EndsWithItsGoAway()
    {
        var request = Concat(Start(), [0x00, 0x00, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        var (connection, _) = await ServeAsync(request);

        AssertGoAway(connection, 0, Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public async Task ServeAsync_MalformedStreamFrame_ResetsThatStream()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1), [0x00, 0x00, 0x04, 0x02, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00]);

        var (connection, _) = await ServeAsync(request);

        Assert.AreEqual(Http2ErrorCode.FrameSizeError, Responses(connection.WrittenBytes)[3].ResetCode);
        Assert.IsNull(GoAway(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_HeaderBlockNotCompleteWithinTheHeadTimeout_EndsWithGoAwayEnhanceYourCalm()
    {
        var block = new Http2TestClient().Block(RequestFields("GET", "/file.txt"));
        var connection = new InMemoryConnection(
            [Concat(Start(), Frame(new Http2HeadersFrame(1, block, EndStream: true, EndHeaders: false)))],
            peerHalfClosesWhenExhausted: false);
        var clock = new ManualTimeProvider(Now);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, TestContext.CancellationToken));

        await WaitForTimersAsync(clock, 1);
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        AssertGoAway(connection, 1, Http2ErrorCode.EnhanceYourCalm);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledDuringAHeaderBlock_SaysGoodbyeWithNoError()
    {
        var block = new Http2TestClient().Block(RequestFields("GET", "/file.txt"));
        var (connection, exchange, serving) = StartServing(Concat(Start(), Frame(new Http2HeadersFrame(1, block, EndStream: true, EndHeaders: false))));

        await WaitUntilAsync(() => Frames(connection.WrittenBytes).Count == 2);
        await exchange.CancelAsync();
        await serving;

        AssertGoAway(connection, 1, Http2ErrorCode.NoError);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesMidFrame_EndsQuietly()
    {
        var (connection, log) = await ServeAsync(Concat(Start(), [0x00, 0x00, 0x08, 0x06]));

        Assert.IsNull(GoAway(connection.WrittenBytes));
        CollectionAssert.Contains(log.Notes.ToArray(), "The client closed the HTTP/2 connection: ConnectionClosedMidFrame.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientSettings_AreAppliedAndAcknowledgedOnce()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Start(new Http2Setting(Http2SettingIdentifier.HeaderTableSize, 0), new Http2Setting(Http2SettingIdentifier.MaxFrameSize, 20000)),
            Frame(Http2SettingsFrame.Acknowledgement),
            client.Get(1));

        var (connection, _) = await ServeAsync(request);

        var frames = Frames(connection.WrittenBytes);
        Assert.AreEqual(1, frames.OfType<Http2SettingsFrame>().Count(settings => settings.Ack));
        Assert.AreEqual(0x20, frames.OfType<Http2HeadersFrame>().Single().FieldBlockFragment[0], "The encoder tells the client its table is now size 0.");
    }

    [TestMethod]
    public async Task ServeAsync_InitialWindowSizeOverflowingAStreamsWindow_EndsWithGoAwayFlowControlError()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Start(),
            client.Get(1),
            Frame(new Http2WindowUpdateFrame(1, int.MaxValue - Http2SendWindows.DefaultInitialWindowSize)),
            Settings(new Http2Setting(Http2SettingIdentifier.InitialWindowSize, Http2SendWindows.DefaultInitialWindowSize + 1)));

        var (connection, _) = await ServeAsync(request, policy: new UnitTestStallingAuthenticationPolicy());

        AssertGoAway(connection, 1, Http2ErrorCode.FlowControlError);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionWindowUpdatePastTheMaximum_EndsWithGoAwayFlowControlError()
    {
        var (connection, _) = await ServeAsync(Concat(Start(), Frame(new Http2WindowUpdateFrame(0, int.MaxValue))));

        AssertGoAway(connection, 0, Http2ErrorCode.FlowControlError);
    }

    [TestMethod]
    public async Task ServeAsync_StreamWindowUpdates_ResetAnOverflowAndIgnoreAClosedStream()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Start(),
            client.Get(1),
            Frame(new Http2WindowUpdateFrame(1, int.MaxValue)),
            Frame(new Http2WindowUpdateFrame(1, 1)));

        var (connection, _) = await ServeAsync(request, policy: new UnitTestStallingAuthenticationPolicy());

        var responses = Responses(connection.WrittenBytes);
        Assert.AreEqual(Http2ErrorCode.FlowControlError, responses[1].ResetCode);
        Assert.AreEqual(1, Frames(connection.WrittenBytes).OfType<Http2RstStreamFrame>().Count());
        Assert.IsNull(GoAway(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("WINDOW_UPDATE")]
    [DataRow("RST_STREAM")]
    [DataRow("DATA")]
    public async Task ServeAsync_FrameOnAStreamNeverOpened_EndsWithGoAwayProtocolError(string frameType)
    {
        var frame = frameType switch
        {
            "WINDOW_UPDATE" => Frame(new Http2WindowUpdateFrame(5, 1)),
            "RST_STREAM" => Frame(new Http2RstStreamFrame(5, Http2ErrorCode.Cancel)),
            _ => Data(5, "x", true),
        };

        var (connection, _) = await ServeAsync(Concat(Start(), frame));

        AssertGoAway(connection, 0, Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public async Task ServeAsync_ClientResetsAnOpenStream_EndsItQuietly()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1), Frame(new Http2RstStreamFrame(1, Http2ErrorCode.Cancel)));

        var (connection, log) = await ServeAsync(request, policy: new UnitTestStallingAuthenticationPolicy());

        CollectionAssert.Contains(log.Notes.ToArray(), "The client reset stream 1 with Cancel.");
        Assert.IsFalse(Responses(connection.WrittenBytes).ContainsKey(1));
    }

    [TestMethod]
    public async Task ServeAsync_ClientResetsAClosedStream_IsIgnored()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1), Frame(new Http2RstStreamFrame(1, Http2ErrorCode.Cancel)));

        var (connection, log) = await ServeAsync(request);

        Assert.AreEqual("200", Responses(connection.WrittenBytes)[1].Status);
        CollectionAssert.DoesNotContain(log.Notes.ToArray(), "The client reset stream 1 with Cancel.");
    }

    [TestMethod]
    [DataRow("PUSH_PROMISE")]
    [DataRow("even stream")]
    public async Task ServeAsync_FrameOnlyAServerSends_EndsWithGoAwayProtocolError(string frameType)
    {
        var client = new Http2TestClient();
        var frame = frameType == "PUSH_PROMISE"
            ? Frame(new Http2PushPromiseFrame(1, 2, [0x82], EndHeaders: true))
            : client.Get(2);

        var (connection, _) = await ServeAsync(Concat(Start(), frame));

        AssertGoAway(connection, 0, Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public async Task ServeAsync_TrailersEndingTheStream_EndTheBodyAndTheRequestIsAnswered()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, endStream: false), client.Headers(1, [new("x-trailer", "1")], endStream: true));

        var (connection, _) = await ServeAsync(request);

        Assert.AreEqual("200", Responses(connection.WrittenBytes)[1].Status);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public async Task ServeAsync_TrailersNotEndingTheStreamOrAfterItsEnd_ResetTheStreamProtocolError(bool requestEndsStream, bool trailersEndStream)
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, endStream: requestEndsStream), client.Headers(1, [new("x-trailer", "1")], trailersEndStream));

        var (connection, _) = await ServeAsync(request, policy: new UnitTestStallingAuthenticationPolicy());

        Assert.AreEqual(Http2ErrorCode.ProtocolError, Responses(connection.WrittenBytes)[1].ResetCode);
    }

    [TestMethod]
    public async Task ServeAsync_HeaderBlockOnAClosedStream_IsIgnored()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1), client.Get(3), client.Headers(1, [new("x-trailer", "1")], endStream: true));

        var (connection, log) = await ServeAsync(request);

        Assert.AreEqual("200", Responses(connection.WrittenBytes)[3].Status);
        CollectionAssert.Contains(log.Notes.ToArray(), "A header block on stream 1, which is closed, was ignored.");
    }

    [TestMethod]
    public async Task ServeAsync_PriorityNamingItsOwnStream_ResetsItProtocolError()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Headers(1, RequestFields("GET", "/file.txt"), endStream: true, new Http2Priority(false, 1, 16)));

        var (connection, _) = await ServeAsync(request);

        Assert.AreEqual(Http2ErrorCode.ProtocolError, Responses(connection.WrittenBytes)[1].ResetCode);
    }

    [TestMethod]
    public async Task ServeAsync_DataNotAddingUpToTheContentLength_ResetsTheStreamProtocolError()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, "/file.txt", false, ("content-length", "5")), Data(1, "abc", true));

        var (connection, _) = await ServeAsync(request);

        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual(Http2ErrorCode.ProtocolError, response.ResetCode);
        Assert.IsEmpty(response.HeaderBlocks);
    }

    [TestMethod]
    public async Task ServeAsync_RefusalWhileTheBodyIsStillArriving_IsFollowedByRstStreamNoErrorAndLaterDataIsIgnored()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Headers(1, RequestFields("POST", "/file.txt"), endStream: false), Data(1, "abc", true));

        var (connection, _) = await ServeAsync(request);

        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual("405", response.Status);
        Assert.AreEqual(Http2ErrorCode.NoError, response.ResetCode);
        Assert.IsTrue(Frames(connection.WrittenBytes).Contains(new Http2WindowUpdateFrame(0, 3)), "DATA on a closed stream still counts against the connection's window.");
    }

    [TestMethod]
    public async Task ServeAsync_EmptyDataEndingTheBody_SendsNoWindowUpdate()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, endStream: false), Data(1, string.Empty, true));

        var (connection, _) = await ServeAsync(request);

        Assert.AreEqual("200", Responses(connection.WrittenBytes)[1].Status);
        Assert.IsFalse(Frames(connection.WrittenBytes).OfType<Http2WindowUpdateFrame>().Any());
    }

    [TestMethod]
    public async Task ServeAsync_DataAfterTheStreamsEnd_ResetsItStreamClosed()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1), Data(1, "x", true));

        var (connection, _) = await ServeAsync(request, policy: new UnitTestStallingAuthenticationPolicy());

        Assert.AreEqual(Http2ErrorCode.StreamClosed, Responses(connection.WrittenBytes)[1].ResetCode);
    }

    [TestMethod]
    public async Task ServeAsync_DataPastTheStreamsReceiveWindow_EndsWithGoAwayFlowControlError()
    {
        var client = new Http2TestClient();
        var chunk = new string('a', Http2FrameWriter.MaxPayloadLength);
        var request = Concat([Start(), client.Get(1, endStream: false), .. Enumerable.Repeat(Data(1, chunk, false), 5)]);

        var (connection, _) = await ServeAsync(request, policy: new UnitTestStallingAuthenticationPolicy());

        AssertGoAway(connection, 1, Http2ErrorCode.FlowControlError);
    }

    [TestMethod]
    public async Task ServeAsync_PaddedData_ReplenishesThePaddingAtOnceAndTheContentAsItIsRead()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, endStream: false), Frame(new Http2DataFrame(1, Ascii("abc"), true, PaddingLength: 4)));

        var (connection, _) = await ServeAsync(request);

        var updates = Frames(connection.WrittenBytes).OfType<Http2WindowUpdateFrame>().ToArray();
        CollectionAssert.AreEquivalent(new[] { new Http2WindowUpdateFrame(0, 8), new Http2WindowUpdateFrame(1, 5), new Http2WindowUpdateFrame(1, 3) }, updates);
        Assert.AreEqual("200", Responses(connection.WrittenBytes)[1].Status);
    }

    [TestMethod]
    public async Task ServeAsync_RequestExpectingContinue_GetsAnInterim100BeforeItsBodyIsRead()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, "/file.txt", false, ("expect", "100-continue")), Data(1, "abc", true));

        var (connection, _) = await ServeAsync(request);

        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual("100", response.HeaderBlocks[0].Single().Value);
        Assert.AreEqual("200", response.Status);
    }

    [TestMethod]
    public async Task ServeAsync_EndedBodyPastTheUploadLimit_IsAnswered413()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, endStream: false), Data(1, "abc", true));

        var (connection, _) = await ServeAsync(request, ExchangeLimits.Default with { MaxUploadBytes = 2 });

        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual("413", response.Status);
        Assert.IsNull(response.ResetCode);
    }

    [TestMethod]
    public async Task ServeAsync_ArrivingBodyPastTheUploadLimit_IsAnswered413ThenRstStreamNoError()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, endStream: false), Data(1, "abc", false));
        var (connection, exchange, serving) = StartServing(request, ExchangeLimits.Default with { MaxUploadBytes = 2 });

        await WaitUntilAsync(() => Responses(connection.WrittenBytes).TryGetValue(1, out var reset) && reset.ResetCode is not null);
        await exchange.CancelAsync();
        await serving;

        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual("413", response.Status);
        Assert.AreEqual(Http2ErrorCode.NoError, response.ResetCode);
    }

    [TestMethod]
    [DataRow("GET", 5, "Hello", true)]
    [DataRow("GET", 0, "", false)]
    [DataRow("HEAD", 5, "", false)]
    public async Task ServeAsync_FileOrHead_EndsTheStreamOnItsLastFrame(string method, int fileLength, string body, bool endsOnData)
    {
        var fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "file.txt"), Ascii("Hello")[..fileLength], FileTime);
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Headers(1, RequestFields(method, "/file.txt"), endStream: true));

        var (connection, _) = await ServeAsync(request, fileSystem: fileSystem);

        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual(body, response.BodyText);
        Assert.AreEqual(endsOnData, response.EndedOnData);
        Assert.AreEqual(!endsOnData, response.EndedOnHeaders);
    }

    [TestMethod]
    [DataRow("shrinks")]
    [DataRow("cannot be read")]
    [DataRow("access denied")]
    public async Task ServeAsync_FileThatCannotBeSentWhole_ResetsTheStreamInternalErrorAndTheConnectionCarriesOn(string failure)
    {
        var path = Path.Join(Root, "file.txt");
        var fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(path, Ascii(FileBody), FileTime, reportedLength: failure == "shrinks" ? FileBody.Length + 5 : null);
        if (failure != "shrinks")
        {
            fileSystem.FailOn(
                path,
                nameof(IContentFileSystem.OpenFileForAsyncRead),
                failure == "cannot be read" ? new IOException("The disk failed.") : new UnauthorizedAccessException("Access is denied."));
        }

        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1), Frame(new Http2PingFrame(false, 1)));

        var (connection, log) = await ServeAsync(request, fileSystem: fileSystem);

        Assert.AreEqual(Http2ErrorCode.InternalError, Responses(connection.WrittenBytes)[1].ResetCode);
        Assert.IsTrue(log.Notes.Any(note => note.EndsWith("stream 1 was reset with INTERNAL_ERROR.", StringComparison.Ordinal)));
        Assert.IsNull(GoAway(connection.WrittenBytes));
    }

    private static (List<string> Fields, string Body) Http11Answer(byte[] response)
    {
        var text = Latin1(response);
        var headEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var fields = text[..headEnd].Split("\r\n").Skip(1)
            .Select(line => line.Split(": ", 2))
            .Select(parts => $"{parts[0].ToLowerInvariant()}: {parts[1]}")
            .ToList();

        return (fields, text[(headEnd + 4)..]);
    }

    private static List<string> RegularFields(Http2TestResponse response) =>
        response.Fields.Where(field => !field.Name.StartsWith(':')).Select(field => $"{field.Name}: {field.Value}").ToList();

    private static void AssertGoAway(InMemoryConnection connection, int lastStreamId, Http2ErrorCode code)
    {
        var goAway = GoAway(connection.WrittenBytes);
        Assert.IsNotNull(goAway, "The server sent no GOAWAY.");
        Assert.AreEqual(lastStreamId, goAway.LastStreamId);
        Assert.AreEqual(code, goAway.ErrorCode);
        Assert.IsTrue(connection.WritesCompleted);
    }

    private static HttpProtocolServer Server(IAuthenticationPolicy? policy = null, IContentFileSystem? fileSystem = null) =>
        new(new ContentStore(Root, fileSystem ?? StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot), policy ?? new AnonymousAuthenticationPolicy());

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        byte[] request, ExchangeLimits? limits = null, IAuthenticationPolicy? policy = null, IContentFileSystem? fileSystem = null)
    {
        var connection = new InMemoryConnection([request]);
        var log = new RecordingExchangeLog();

        await Server(policy, fileSystem).ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken, limits));

        return (connection, log);
    }

    // A connection the client keeps open, served until the test cancels the exchange as a limit would.
    private (InMemoryConnection Connection, CancellationTokenSource Exchange, Task Serving) StartServing(byte[] request, ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection([request], peerHalfClosesWhenExhausted: false);
        var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        TestContext.CancellationToken.Register(exchange.Dispose);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), exchange.Token, limits));

        return (connection, exchange, serving);
    }
}
