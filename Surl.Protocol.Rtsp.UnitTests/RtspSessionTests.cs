using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Rtsp.PatternRandomNumberGenerator;
using static Surl.Protocol.Rtsp.RtspServerHarness;
using static Surl.Protocol.Rtsp.ScriptedConnection;

namespace Surl.Protocol.Rtsp;

[TestClass]
public sealed class RtspSessionTests
{
    private const string SessionField = "Session: " + SessionId;

    private const string SetupLong = "SETUP rtsp://h/long.bin RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;unicast;interleaved=0-1\r\n\r\n";

    private const string SetupClip = "SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;unicast;interleaved=0-1\r\n\r\n";

    private static readonly DateTimeOffset NtpEpoch = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SetupPlayPausePlayTeardown_AnswersEachWithOneSessionAndStreamsTheFileOnce()
    {
        var connection = new ScriptedConnection(
            Immediately(SetupLong),
            Immediately($"PLAY rtsp://h/long.bin RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\nRange: npt=0-\r\n\r\n"),
            When($"PAUSE rtsp://h/long.bin RTSP/1.0\r\nCSeq: 3\r\n{SessionField}\r\n\r\n", written => RtspOutput.FramesOn(written, 0) >= 1),
            When($"PLAY * RTSP/1.0\r\nCSeq: 4\r\n{SessionField}\r\n\r\n", written => RtspOutput.Heads(written).Count == 3),
            When($"TEARDOWN rtsp://h/long.bin RTSP/1.0\r\nCSeq: 5\r\n{SessionField}\r\n\r\n", written => RtspOutput.FramesOn(written, 1) == 1));
        var log = new RecordingExchangeLog();

        await Server().ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken));

        var items = RtspOutput.Parse(connection.WrittenBytes);
        var heads = items.OfType<string>().ToList();
        var pausedAfter = items.IndexOf(heads[2]) - items.IndexOf(heads[1]) - 1;
        Assert.IsTrue(pausedAfter is >= 1 and < 51, $"PAUSE was answered after {pausedAfter} packets.");
        Assert.HasCount(5, heads);
        Assert.AreEqual(ResponseHead("200 OK", "1", SessionField + ";timeout=60", "Transport: RTP/AVP/TCP;unicast;interleaved=0-1;ssrc=01234567"), heads[0]);
        Assert.AreEqual(ResponseHead("200 OK", "2", SessionField, "Range: npt=0-", $"RTP-Info: url=rtsp://h/long.bin;seq={FirstSequenceNumber};rtptime={FirstTimestamp}"), heads[1]);
        Assert.AreEqual(ResponseHead("200 OK", "3", SessionField), heads[2]);
        Assert.AreEqual(ResponseHead("200 OK", "4", SessionField, "Range: npt=0-", $"RTP-Info: url=*;seq={FirstSequenceNumber + pausedAfter};rtptime={FirstTimestamp + (uint)(1400 * pausedAfter)}"), heads[3]);
        Assert.AreEqual(ResponseHead("200 OK", "5"), heads[4]);
        Assert.AreSame(heads[3], items[items.IndexOf(heads[2]) + 1], "Nothing is streamed while paused.");
        Assert.AreSame(heads[4], items[^1]);

        var packets = items.OfType<RtspOutput.Frame>().Where(frame => frame.Channel == 0).ToList();
        AssertStreamsTheFile(packets, LongClip);
        var report = items.OfType<RtspOutput.Frame>().Single(frame => frame.Channel == 1);
        Assert.AreSame(report, items[^2], "The RTCP report follows the last packet.");
        AssertSenderReportAndBye(report, FirstTimestamp + (50 * 1400), 51, (uint)LongClip.Length);

        CollectionAssert.AreEqual(
            new[]
            {
                $"RTSP session {SessionId} set up for /long.bin, interleaved 0-1",
                $"Streamed {1400 * pausedAfter} bytes of /long.bin, paused after {pausedAfter} packets",
                $"Streamed {LongClip.Length - (1400 * pausedAfter)} bytes of /long.bin in {51 - pausedAfter} RTP packets",
                $"RTSP session {SessionId} ended: TEARDOWN",
                "The client closed the connection: ConnectionClosed.",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Play_OverAnInMemoryConnection_StreamsTheFileOnTheNegotiatedChannelsThenWaitsForTheNextRequest()
    {
        var setup = "SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;unicast;interleaved=4-5\r\n\r\n";
        var play = $"PLAY rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n";

        var (items, log) = await PlayUntilCancelledAsync(setup + play, StandardFileSystem());

        var packet = (RtspOutput.Frame)items[2];
        Assert.AreEqual(4, packet.Channel);
        AssertStreamsTheFile([packet], Ascii(ClipBody));
        var report = (RtspOutput.Frame)items[3];
        Assert.AreEqual(5, report.Channel);
        AssertSenderReportAndBye(report, FirstTimestamp, 1, 10);
        Assert.HasCount(4, items);
        CollectionAssert.AreEqual(
            new[]
            {
                $"RTSP session {SessionId} set up for /clip.bin, interleaved 4-5",
                "Streamed 10 bytes of /clip.bin in 1 RTP packets",
                $"RTSP session {SessionId} ended: connection closed",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Play_OfAnEmptyFileTwice_SendsOneEmptyMarkedPacketEachTimeWithTheNumbersRunningOn()
    {
        var connection = new ScriptedConnection(
            Immediately("SETUP rtsp://h/empty.bin RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP\r\n\r\n"),
            Immediately($"PLAY * RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n"),
            When($"PLAY * RTSP/1.0\r\nCSeq: 3\r\n{SessionField}\r\n\r\n", written => RtspOutput.FramesOn(written, 1) == 1));

        await Server().ServeAsync(connection, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken));

        var items = RtspOutput.Parse(connection.WrittenBytes);
        var frames = items.OfType<RtspOutput.Frame>().ToList();
        Assert.HasCount(4, frames);
        Assert.AreEqual($"RTP-Info: url=*;seq={FirstSequenceNumber + 1};rtptime={FirstTimestamp}", RtspOutput.Heads(connection.WrittenBytes)[2].Split("\r\n")[6]);
        AssertStreamsTheFile([frames[0]], []);
        Assert.IsTrue(frames[2].Marker);
        Assert.IsEmpty(frames[2].Payload);
        Assert.AreEqual((ushort)(FirstSequenceNumber + 1), frames[2].SequenceNumber);
        Assert.AreEqual(FirstTimestamp, frames[2].Timestamp);
        AssertSenderReportAndBye(frames[3], FirstTimestamp, 2, 0);
    }

    [TestMethod]
    public async Task Play_OfAFileThatShrank_EndsWithAnEmptyMarkedPacketWhereItsBytesRanOut()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "clip.bin"), Ascii(ClipBody), Now, reportedLength: 3000);

        var (items, log) = await PlayUntilCancelledAsync(SetupClip + $"PLAY * RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n", fileSystem);

        var frames = items.OfType<RtspOutput.Frame>().ToList();
        Assert.HasCount(3, frames);
        AssertStreamsTheFile(frames.Take(2).ToList(), Ascii(ClipBody));
        AssertSenderReportAndBye(frames[2], FirstTimestamp + 10, 2, 10);
        Assert.AreEqual("Streamed 10 bytes of /clip.bin in 2 RTP packets", log.Notes[1]);
    }

    [TestMethod]
    [DataRow(typeof(IOException))]
    [DataRow(typeof(UnauthorizedAccessException))]
    public async Task Play_OfAFileThatCannotBeRead_SendsOneEmptyMarkedPacket(Type failureType)
    {
        var failure = (Exception)Activator.CreateInstance(failureType, "gone")!;
        var fileSystem = StandardFileSystem().FailOn(Path.Join(Root, "clip.bin"), nameof(UnitTestInMemoryContentFileSystem.OpenFileForAsyncRead), failure);

        var (items, _) = await PlayUntilCancelledAsync(SetupClip + $"PLAY * RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n", fileSystem);

        var frames = items.OfType<RtspOutput.Frame>().ToList();
        Assert.HasCount(2, frames);
        AssertStreamsTheFile(frames.Take(1).ToList(), []);
    }

    [TestMethod]
    public async Task Play_OfAFileWhoseStatusCanNoLongerBeRead_Is404WithTheSession()
    {
        var fileSystem = StandardFileSystem();
        var released = false;
        var connection = new ScriptedConnection(Immediately(SetupClip), When($"PLAY * RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n", _ => released));
        var log = new RecordingExchangeLog();
        var serving = Server(fileSystem).ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken));

        fileSystem.FailOn(Path.Join(Root, "clip.bin"), nameof(UnitTestInMemoryContentFileSystem.GetFileLength), new IOException("gone"));
        released = true;
        connection.Recheck();
        await serving;

        Assert.AreEqual(ResponseHead("404 Not Found", "2", SessionField), RtspOutput.Heads(connection.WrittenBytes)[1]);
        Assert.AreEqual($"RTSP PLAY refused: 404 Not Found: {Path.Join(Root, "clip.bin")} could not be read (IOException: gone)", log.Notes[1]);
    }

    [TestMethod]
    public async Task Play_ConnectionResetWhileStreaming_StopsStreamingAndEndsTheSession()
    {
        var connection = new ScriptedConnection(Immediately(SetupLong), Immediately($"PLAY * RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n")) { ResetWhenDone = true };
        var log = new RecordingExchangeLog();

        await Assert.ThrowsExactlyAsync<IOException>(() => Server().ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken)));

        Assert.IsEmpty(RtspOutput.Parse(connection.WrittenBytes).OfType<RtspOutput.Frame>());
        Assert.AreEqual($"RTSP session {SessionId} ended: connection closed", log.Notes[^1]);
    }

    [TestMethod]
    public async Task Setup_WhilePlaying_Is455WithTheSessionAndStreamingCarriesOn()
    {
        var connection = new ScriptedConnection(
            Immediately(SetupLong),
            Immediately($"PLAY * RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n"),
            When($"SETUP rtsp://h/long.bin RTSP/1.0\r\nCSeq: 3\r\n{SessionField}\r\nTransport: RTP/AVP/TCP\r\n\r\n", written => RtspOutput.FramesOn(written, 0) >= 1));
        var log = new RecordingExchangeLog();

        await Server().ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken));

        Assert.AreEqual(ResponseHead("455 Method Not Valid in This State", "3", SessionField), RtspOutput.Heads(connection.WrittenBytes)[2]);
        Assert.AreEqual("RTSP SETUP refused: 455 Method Not Valid in This State: the session is playing", log.Notes[1]);
        AssertStreamsTheFile(RtspOutput.Parse(connection.WrittenBytes).OfType<RtspOutput.Frame>().Where(frame => frame.Channel == 0).ToList(), LongClip);
    }

    [TestMethod]
    public async Task Session_IdleForItsTimeout_HasEndedAndASetupMakesANewOne()
    {
        var clock = new ManualTimeProvider(Now);
        var released = false;
        var connection = new ScriptedConnection(
            Immediately(SetupClip),
            When($"GET_PARAMETER * RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n" + SetupClip.Replace("CSeq: 1", "CSeq: 3", StringComparison.Ordinal), _ => released));
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        clock.Advance(RtspSession.Timeout);
        released = true;
        connection.Recheck();
        await serving;

        var heads = RtspOutput.Heads(connection.WrittenBytes);
        Assert.StartsWith("RTSP/1.0 454 Session Not Found\r\nCSeq: 2\r\n", heads[1]);
        Assert.DoesNotContain("Session:", heads[1]);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 3\r\n", heads[2]);
        Assert.AreEqual(60, RtspSession.Timeout.TotalSeconds);
        Assert.AreEqual($"RTSP session {SessionId} ended: timeout", log.Notes[1]);
        Assert.AreEqual("RTSP GET_PARAMETER refused: 454 Session Not Found: the connection holds no session of that ID", log.Notes[2]);
    }

    [TestMethod]
    public async Task Session_NamedByARequestBeforeItsTimeout_LivesAnotherTimeout()
    {
        var clock = new ManualTimeProvider(Now);
        var step = 0;
        var almostTimeout = RtspSession.Timeout - TimeSpan.FromSeconds(1);
        var connection = new ScriptedConnection(
            Immediately(SetupClip),
            When($"GET_PARAMETER * RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n", _ => step >= 1),
            When($"PAUSE * RTSP/1.0\r\nCSeq: 3\r\n{SessionField}\r\n\r\n", _ => step >= 2));
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, TestContext.CancellationToken));

        clock.Advance(almostTimeout);
        step = 1;
        connection.Recheck();
        clock.Advance(almostTimeout);
        step = 2;
        connection.Recheck();
        await serving;

        var heads = RtspOutput.Heads(connection.WrittenBytes);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 2\r\n", heads[1]);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 3\r\n", heads[2]);
        Assert.Contains(SessionField + "\r\n", heads[2]);
    }

    [TestMethod]
    [DataRow("SETUP * RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP/TCP\r\n\r\n", "400 Bad Request", "* names no presentation")]
    [DataRow("SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\n\r\n", "400 Bad Request", "SETUP needs a Transport")]
    [DataRow("SETUP rtsp://h/missing RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP/TCP\r\n\r\n", "404 Not Found", "no file exists at ")]
    [DataRow("SETUP rtsp://h/%2e%2e/x RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP/TCP\r\n\r\n", "404 Not Found", "the path was refused (")]
    [DataRow("SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP;unicast;client_port=5000-5001\r\n\r\n", "461 Unsupported Transport", "no RTP/AVP/TCP alternative")]
    [DataRow("SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP/TCP;multicast\r\n\r\n", "461 Unsupported Transport", "no RTP/AVP/TCP alternative")]
    [DataRow("SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP/TCP;interleaved=1-2\r\n\r\n", "461 Unsupported Transport", "the interleaved channels are not an even channel and the one after it, both below 256")]
    [DataRow("SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP/TCP;mode=receive\r\n\r\n", "461 Unsupported Transport", "the mode is neither play nor record")]
    [DataRow("SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP/TCP;mode=record\r\n\r\n", "403 Forbidden", "uploads are not allowed")]
    [DataRow("PLAY * RTSP/1.0\r\nCSeq: 2\r\n\r\n", "454 Session Not Found", "the request names no session")]
    [DataRow("PAUSE rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\n\r\n", "454 Session Not Found", "the request names no session")]
    [DataRow("TEARDOWN * RTSP/1.0\r\nCSeq: 2\r\n\r\n", "454 Session Not Found", "the request names no session")]
    [DataRow("OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nSession: 0123456789ABCDEF\r\n\r\n", "454 Session Not Found", "the connection holds no session of that ID")]
    public async Task Request_WithNoSessionHeld_IsRefusedAsTheAdrSays(string request, string statusLine, string checkPrefix)
    {
        var (connection, log) = await ServeAsync([Ascii(request)], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead(statusLine, "2"), Latin1(connection.WrittenBytes));
        Assert.StartsWith($"RTSP {request.Split(' ')[0]} refused: {statusLine}: {checkPrefix}", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP/TCP\r\n\r\n", "455 Method Not Valid in This State", null, "the connection already holds a session")]
    [DataRow("SETUP rtsp://h/long.bin RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\nTransport: RTP/AVP/TCP\r\n\r\n", "455 Method Not Valid in This State", SessionField, "the session holds another presentation")]
    [DataRow("PLAY * RTSP/1.0\r\nCSeq: 2\r\nSession: 0000000000000000\r\n\r\n", "454 Session Not Found", null, "the connection holds no session of that ID")]
    [DataRow("PLAY * RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\nRange: npt=10-\r\n\r\n", "457 Invalid Range", SessionField, "only npt=0- and npt=now- are served")]
    [DataRow("PLAY rtsp://h/long.bin RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\n\r\n", "404 Not Found", SessionField, "the Request-URI names another presentation than the session's")]
    [DataRow("PAUSE rtsp://h/%2e%2e/x RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\n\r\n", "404 Not Found", SessionField, "the Request-URI names another presentation than the session's")]
    [DataRow("GET_PARAMETER * RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\nContent-Length: 7\r\n\r\npackets", "451 Parameter Not Understood", SessionField, "surl has no parameters to report or set")]
    [DataRow("SET_PARAMETER * RTSP/1.0\r\nCSeq: 2\r\nContent-Length: 7\r\n\r\nx: 1\r\n\r\n", "451 Parameter Not Understood", null, "surl has no parameters to report or set")]
    public async Task Request_OnTheHeldSession_IsRefusedAsTheAdrSays(string request, string statusLine, string? sessionField, string check)
    {
        var (connection, log) = await ServeAsync([Ascii(SetupClip + request)], TestContext.CancellationToken);

        var expected = sessionField is null ? ResponseHead(statusLine, "2") : ResponseHead(statusLine, "2", sessionField);
        Assert.AreEqual(expected, RtspOutput.Heads(connection.WrittenBytes)[1]);
        Assert.AreEqual($"RTSP {request.Split(' ')[0]} refused: {statusLine}: {check}", log.Notes[1]);
    }

    [TestMethod]
    [DataRow("PAUSE rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + ";timeout=60\r\n\r\n")]
    [DataRow("GET_PARAMETER * RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\n\r\n")]
    [DataRow("SET_PARAMETER rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\n\r\n")]
    [DataRow("OPTIONS * RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\n\r\n")]
    public async Task Request_NamingTheHeldSession_Is200AndNamesItBack(string request)
    {
        var (connection, _) = await ServeAsync([Ascii(SetupClip + request)], TestContext.CancellationToken);

        var answer = RtspOutput.Heads(connection.WrittenBytes)[1];
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 2\r\n", answer);
        Assert.Contains("\r\nServer: surl\r\n" + SessionField + "\r\n", answer);
    }

    [TestMethod]
    public async Task Describe_NamingTheHeldSession_NamesItBack()
    {
        var (connection, _) = await ServeAsync([Ascii(SetupClip + $"DESCRIBE rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n")], TestContext.CancellationToken);

        Assert.Contains("\r\nCSeq: 2\r\n" + DateField + "Server: surl\r\n" + SessionField + "\r\nContent-Type: application/sdp\r\n", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task GetParameter_WithNoBodyAndNoSession_Is200WithNoSession()
    {
        var (connection, _) = await ServeAsync([Ascii("GET_PARAMETER * RTSP/1.0\r\nCSeq: 2\r\n\r\n")], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead("200 OK", "2"), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task Setup_AgainOnTheReadySession_RenegotiatesTheTransportAndPlaysOnIt()
    {
        var resetup = $"SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\nTransport: RTP/AVP;unicast, RTP/AVP/TCP;interleaved=254-255;mode=\"PLAY\"\r\n\r\n";
        var play = $"PLAY rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 3\r\n{SessionField}\r\nRange: npt=now-\r\n\r\n";

        var (items, log) = await PlayUntilCancelledAsync(SetupClip + resetup + play, StandardFileSystem());

        Assert.AreEqual(ResponseHead("200 OK", "2", SessionField + ";timeout=60", "Transport: RTP/AVP/TCP;unicast;interleaved=254-255;ssrc=01234567"), items[1]);
        Assert.AreEqual(254, ((RtspOutput.Frame)items[3]).Channel);
        Assert.AreEqual(255, ((RtspOutput.Frame)items[4]).Channel);
        Assert.AreEqual($"RTSP session {SessionId} set up for /clip.bin, interleaved 254-255", log.Notes[1]);
    }

    [TestMethod]
    public async Task Teardown_EndsTheSessionSoItIsNotFoundAfterwardsAndANewSetupSucceeds()
    {
        var requests = SetupClip
            + $"TEARDOWN rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 2\r\n{SessionField}\r\n\r\n"
            + $"PLAY * RTSP/1.0\r\nCSeq: 3\r\n{SessionField}\r\n\r\n"
            + SetupClip.Replace("CSeq: 1", "CSeq: 4", StringComparison.Ordinal);

        var (connection, log) = await ServeAsync([Ascii(requests)], TestContext.CancellationToken);

        var heads = RtspOutput.Heads(connection.WrittenBytes);
        Assert.AreEqual(ResponseHead("200 OK", "2"), heads[1]);
        Assert.AreEqual(ResponseHead("454 Session Not Found", "3"), heads[2]);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 4\r\n", heads[3]);
        Assert.AreEqual($"RTSP session {SessionId} ended: TEARDOWN", log.Notes[1]);
        Assert.AreEqual($"RTSP session {SessionId} ended: connection closed", log.Notes[^1]);
    }

    [TestMethod]
    public async Task Constructor_WithNoRandomSourceGiven_DrawsSessionIdsFromTheSystemGenerator()
    {
        var server = new RtspProtocolServer(new ContentStore(Root, StandardFileSystem(), new ContentExposureOptions()), new AnonymousAuthenticationPolicy());
        var connection = new InMemoryConnection([Ascii(SetupClip)]);

        await server.ServeAsync(connection, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken));

        Assert.MatchesRegex(@"\r\nSession: [0-9A-F]{16};timeout=60\r\nTransport: RTP/AVP/TCP;unicast;interleaved=0-1;ssrc=[0-9A-F]{8}\r\n", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public void Constructor_WithNoRandomSource_IsRefused()
    {
        var contentStore = new ContentStore(Root, StandardFileSystem(), new ContentExposureOptions());

        Assert.ThrowsExactly<ArgumentNullException>(() => new RtspProtocolServer(contentStore, new AnonymousAuthenticationPolicy(), null!));
    }

    // Serves the requests over an InMemoryConnection whose client never half-closes, so the
    // server streams what PLAY asked for and then waits for a request that never comes; the
    // exchange is then cancelled.
    private async Task<(IReadOnlyList<object> Items, RecordingExchangeLog Log)> PlayUntilCancelledAsync(string requests, UnitTestInMemoryContentFileSystem fileSystem)
    {
        var connection = new InMemoryConnection([Ascii(requests)], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server(fileSystem).ServeAsync(connection, Context(log, new ManualTimeProvider(Now), exchange.Token));

        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);

        return (RtspOutput.Parse(connection.WrittenBytes), log);
    }

    private static void AssertStreamsTheFile(IReadOnlyList<RtspOutput.Frame> packets, byte[] file)
    {
        CollectionAssert.AreEqual(file, packets.SelectMany(packet => packet.Payload).ToArray());
        var timestamp = FirstTimestamp;
        for (var index = 0; index < packets.Count; index++)
        {
            var packet = packets[index];
            Assert.AreEqual(0x80, packet.Packet[0], "Version 2, no padding, no extension, no CSRC.");
            Assert.AreEqual(96, packet.PayloadType);
            Assert.AreEqual(index == packets.Count - 1, packet.Marker, $"Only the last packet is marked (packet {index}).");
            Assert.AreEqual((ushort)(FirstSequenceNumber + index), packet.SequenceNumber);
            Assert.AreEqual(timestamp, packet.Timestamp);
            Assert.AreEqual(Ssrc, packet.Ssrc);
            timestamp += (uint)packet.Payload.Length;
        }
    }

    private static void AssertSenderReportAndBye(RtspOutput.Frame report, uint lastTimestamp, uint packetCount, uint octetCount)
    {
        var sinceNtpEpoch = Now - NtpEpoch;
        CollectionAssert.AreEqual(
            new[] { 0x80C8_0006u, Ssrc, (uint)sinceNtpEpoch.TotalSeconds, 0u, lastTimestamp, packetCount, octetCount, 0x81CB_0001u, Ssrc },
            Enumerable.Range(0, 9).Select(report.Word).ToArray());
        Assert.HasCount(36, report.Packet);
    }
}
