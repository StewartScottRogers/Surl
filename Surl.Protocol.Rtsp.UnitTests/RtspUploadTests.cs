using System.Buffers.Binary;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Rtsp.PatternRandomNumberGenerator;
using static Surl.Protocol.Rtsp.RtspServerHarness;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// <c>ANNOUNCE</c>, <c>SETUP</c> with <c>mode=record</c>, <c>RECORD</c> and the interleaved
/// frames a recording appends (ADR-0074 decision 6), with the requests upstream libcurl 8.21.0
/// sends as ADR-0074's amendment 1 measured them.
/// </summary>
[TestClass]
public sealed class RtspUploadTests
{
    private const string SessionField = "Session: " + SessionId;

    private const string NextOptions = "OPTIONS * RTSP/1.0\r\nCSeq: 9\r\n\r\n";

    // libcurl's ANNOUNCE with CURLOPT_COPYPOSTFIELDS "v=0\r\n" (ADR-0074 amendment 1).
    private const string Announce = "ANNOUNCE rtsp://127.0.0.1:18554/rec.bin RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 5\r\nContent-Type: application/sdp\r\n\r\nv=0\r\n";

    // libcurl's SETUP with CURLOPT_RTSP_TRANSPORT "RTP/AVP/TCP;unicast;interleaved=0-1;mode=record".
    private const string SetupRecord = "SETUP rtsp://127.0.0.1:18554/rec.bin RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;unicast;interleaved=0-1;mode=record\r\n\r\n";

    private const string Record = "RECORD rtsp://127.0.0.1:18554/rec.bin RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\n\r\n";

    private const string RecordStar = "RECORD * RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\n\r\n";

    private const string SetupClipToPlay = "SETUP rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;unicast;interleaved=0-1\r\n\r\n";

    private const string Teardown = "TEARDOWN rtsp://127.0.0.1:18554/rec.bin RTSP/1.0\r\nCSeq: 3\r\n" + SessionField + "\r\n\r\n";

    private static readonly ContentExposureOptions UploadsAllowed = new() { AllowUploads = true };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Announce_WithUploadsAllowed_StoresTheBodyByteForByteBesideThePresentation()
    {
        var fileSystem = UploadFileSystem();

        var (connection, log) = await ServeAsync(OneBytePerRead(Ascii(Announce + NextOptions)), TestContext.CancellationToken, fileSystem: fileSystem, exposureOptions: UploadsAllowed);

        var heads = RtspOutput.Heads(connection.WrittenBytes);
        Assert.AreEqual(ResponseHead("200 OK", "1"), heads[0]);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 9\r\n", heads[1]);
        CollectionAssert.AreEqual(Ascii("v=0\r\n"), StoredBytes(fileSystem, "rec.bin.sdp"));
        Assert.AreEqual("Stored 5 bytes at /rec.bin.sdp", log.Notes[0]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media", "rec.bin.sdp" }, Entries(fileSystem));
    }

    [TestMethod]
    public async Task Announce_WithoutUploads_Is403AndStoresNothing()
    {
        var fileSystem = UploadFileSystem();

        var (connection, log) = await ServeAsync([Ascii(Announce + NextOptions)], TestContext.CancellationToken, fileSystem: fileSystem);

        Assert.AreEqual(ResponseHead("403 Forbidden", "1"), RtspOutput.Heads(connection.WrittenBytes)[0]);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 9\r\n", RtspOutput.Heads(connection.WrittenBytes)[1]);
        Assert.AreEqual("RTSP ANNOUNCE refused: 403 Forbidden: the content store refused an upload to /rec.bin.sdp (NotPermitted)", log.Notes[0]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media" }, Entries(fileSystem));
    }

    [TestMethod]
    [DataRow("ANNOUNCE * RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 3\r\n\r\nv=0", "400 Bad Request", "* names no presentation")]
    [DataRow("ANNOUNCE rtsp://h/rec.bin RTSP/1.0\r\nCSeq: 1\r\n\r\n", "400 Bad Request", "ANNOUNCE needs a description in its body")]
    [DataRow("ANNOUNCE rtsp://h/rec.bin RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 0\r\n\r\n", "400 Bad Request", "ANNOUNCE needs a description in its body")]
    [DataRow("ANNOUNCE rtsp://h/none/rec.bin RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 3\r\n\r\nv=0", "404 Not Found", "the content store refused an upload to /none/rec.bin.sdp (NoSuchDirectory)")]
    [DataRow("ANNOUNCE rtsp://h/media/d RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 3\r\n\r\nv=0", "403 Forbidden", "the content store refused an upload to /media/d.sdp (IsADirectory)")]
    [DataRow("ANNOUNCE rtsp://h/.hidden RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 3\r\n\r\nv=0", "403 Forbidden", "the content store refused an upload to /.hidden.sdp (NotPermitted)")]
    [DataRow("ANNOUNCE rtsp://h/%2e%2e/x RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 3\r\n\r\nv=0", "403 Forbidden", "the content store refused an upload to /%2e%2e/x.sdp (a refused path)")]
    public async Task Announce_TheAdrRefuses_IsRefusedReadingItsBodyAndStoresNothing(string request, string statusLine, string check)
    {
        var fileSystem = UploadFileSystem();
        fileSystem.CreateDirectory(Path.Join(Root, "media", "d.sdp"));

        var (connection, log) = await ServeAsync([Ascii(request + NextOptions)], TestContext.CancellationToken, fileSystem: fileSystem, exposureOptions: UploadsAllowed);

        var heads = RtspOutput.Heads(connection.WrittenBytes);
        Assert.AreEqual(ResponseHead(statusLine, "1"), heads[0]);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 9\r\n", heads[1]);
        Assert.AreEqual($"RTSP ANNOUNCE refused: {statusLine}: {check}", log.Notes[0]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media" }, Entries(fileSystem));
    }

    [TestMethod]
    public async Task Announce_PastTheStoresOwnUploadLimit_Is403AndStoresNothing()
    {
        var fileSystem = UploadFileSystem();

        var (connection, log) = await ServeAsync([Ascii(Announce)], TestContext.CancellationToken, fileSystem: fileSystem, exposureOptions: UploadsAllowed with { MaxUploadBytes = 4 });

        Assert.AreEqual(ResponseHead("403 Forbidden", "1"), Latin1(connection.WrittenBytes));
        Assert.AreEqual("RTSP ANNOUNCE refused: 403 Forbidden: /rec.bin.sdp could not be stored (TooLarge)", log.Notes[0]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media" }, Entries(fileSystem));
    }

    [TestMethod]
    public async Task Announce_PastMaxFilesize_Is413BeforeItsBodyIsReadAndStoresNothing()
    {
        var fileSystem = UploadFileSystem();
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 4 };

        var (connection, log) = await ServeAsync([Ascii(Announce)], TestContext.CancellationToken, limits, fileSystem: fileSystem, exposureOptions: UploadsAllowed);

        Assert.AreEqual(ResponseHead("413 Request Entity Too Large", "1"), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("RTSP ANNOUNCE refused: 413 Request Entity Too Large: the 5-byte body is past the upload limit of 4 bytes", log.Notes[0]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media" }, Entries(fileSystem));
    }

    [TestMethod]
    public async Task Record_SetupRecordPauseRecordTeardown_StoresEveryRtpPayloadOnItsChannelInArrivalOrder()
    {
        var fileSystem = UploadFileSystem();
        var requests = Concat(
            Ascii(SetupRecord.Replace("interleaved=0-1", "interleaved=2-3", StringComparison.Ordinal)),
            Ascii(Record),
            RtpFrame(2, "ab"),
            RtpFrame(2, "cd", csrcCount: 2),
            Frame(3, [0x81, 0xCB, 0x00, 0x01, 0x01, 0x23, 0x45, 0x67]),
            RtpFrame(2, "ef", extensionWords: 2),
            RtpFrame(2, "gh", padding: 3),
            RtpFrame(2, string.Empty),
            Frame(2, [0x40, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'v', (byte)'1']),
            Frame(2, [0x80, 96, 0]),
            Ascii($"PAUSE * RTSP/1.0\r\nCSeq: 4\r\n{SessionField}\r\n\r\n"),
            RtpFrame(2, "paused"),
            Ascii($"RECORD * RTSP/1.0\r\nCSeq: 5\r\n{SessionField}\r\n\r\n"),
            RtpFrame(2, "ij"),
            RtpFrame(7, "other channel"),
            Ascii(Teardown.Replace("CSeq: 3", "CSeq: 6", StringComparison.Ordinal)));

        var (connection, log) = await ServeAsync([requests], TestContext.CancellationToken, fileSystem: fileSystem, exposureOptions: UploadsAllowed);

        var heads = RtspOutput.Heads(connection.WrittenBytes);
        Assert.HasCount(5, heads);
        Assert.AreEqual(ResponseHead("200 OK", "1", SessionField + ";timeout=60", "Transport: RTP/AVP/TCP;unicast;interleaved=2-3;ssrc=01234567;mode=record"), heads[0]);
        Assert.AreEqual(ResponseHead("200 OK", "2", SessionField), heads[1]);
        Assert.AreEqual(ResponseHead("200 OK", "4", SessionField), heads[2]);
        Assert.AreEqual(ResponseHead("200 OK", "5", SessionField), heads[3]);
        Assert.AreEqual(ResponseHead("200 OK", "6"), heads[4]);
        CollectionAssert.AreEqual(Ascii("abcdefghij"), StoredBytes(fileSystem, "rec.bin"));
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media", "rec.bin" }, Entries(fileSystem));
        CollectionAssert.AreEqual(
            new[]
            {
                $"RTSP session {SessionId} set up for /rec.bin, interleaved 2-3",
                "Stored 10 bytes at /rec.bin",
                $"RTSP session {SessionId} ended: TEARDOWN",
                "The client closed the connection: ConnectionClosed.",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Record_AsLibcurlSendsIt_WithNoPackets_ReplacesTheFileWithAnEmptyOne()
    {
        var fileSystem = UploadFileSystem();
        WriteFile(fileSystem, "rec.bin", "old bytes");

        var (connection, _) = await ServeAsync([Ascii(SetupRecord + Record + Teardown)], TestContext.CancellationToken, fileSystem: fileSystem, exposureOptions: UploadsAllowed);

        var heads = RtspOutput.Heads(connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "200", "200", "200" }, heads.Select(head => head[9..12]).ToArray());
        Assert.IsEmpty(StoredBytes(fileSystem, "rec.bin"));
    }

    [TestMethod]
    [DataRow(SetupClipToPlay + RecordStar, "RECORD", "455 Method Not Valid in This State", "the session was set up to play")]
    [DataRow(SetupRecord + "PLAY * RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\n\r\n", "PLAY", "455 Method Not Valid in This State", "the session was set up to record")]
    [DataRow(SetupRecord + "SETUP rtsp://h/rec.bin RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\nTransport: RTP/AVP/TCP\r\n\r\n", "SETUP", "455 Method Not Valid in This State", "a SETUP cannot change whether the session plays or records")]
    [DataRow(SetupRecord + Record + "SETUP rtsp://h/rec.bin RTSP/1.0\r\nCSeq: 2\r\n" + SessionField + "\r\nTransport: RTP/AVP/TCP;mode=record\r\n\r\n", "SETUP", "455 Method Not Valid in This State", "the session is recording")]
    [DataRow("SETUP rtsp://h/none/rec.bin RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;mode=record\r\n\r\n" + RecordStar, "RECORD", "404 Not Found", "the content store refused an upload to /none/rec.bin (NoSuchDirectory)")]
    [DataRow("SETUP rtsp://h/media RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;mode=record\r\n\r\n" + RecordStar, "RECORD", "403 Forbidden", "the content store refused an upload to /media (IsADirectory)")]
    [DataRow("SETUP rtsp://h/%2e%2e/x RTSP/1.0\r\nCSeq: 2\r\nTransport: RTP/AVP/TCP;mode=record\r\n\r\n", "SETUP", "403 Forbidden", "the path was refused (")]
    [DataRow("RECORD * RTSP/1.0\r\nCSeq: 2\r\n\r\n", "RECORD", "454 Session Not Found", "the request names no session")]
    public async Task Record_TheAdrRefuses_IsRefusedAndStoresNothing(string requests, string method, string statusLine, string checkPrefix)
    {
        var fileSystem = UploadFileSystem();

        var (connection, log) = await ServeAsync([Ascii(requests)], TestContext.CancellationToken, fileSystem: fileSystem, exposureOptions: UploadsAllowed);

        var refusal = RtspOutput.Heads(connection.WrittenBytes)[^1];
        Assert.StartsWith($"RTSP/1.0 {statusLine}\r\nCSeq: 2\r\n", refusal);
        Assert.ContainsSingle(log.Notes.Where(note => note.StartsWith($"RTSP {method} refused: {statusLine}: {checkPrefix}", StringComparison.Ordinal)));
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media" }, Entries(fileSystem));
    }

    [TestMethod]
    public async Task Record_GrowingPastTheUploadLimit_IsDiscardedAndTheConnectionClosedWithNoAnswer()
    {
        var fileSystem = UploadFileSystem();
        var requests = Concat(Ascii(SetupRecord + Record), RtpFrame(0, "abc"), RtpFrame(0, "de"), Ascii(NextOptions));

        var (connection, log) = await ServeAsync([requests], TestContext.CancellationToken, peerHalfCloses: false, fileSystem: fileSystem, exposureOptions: UploadsAllowed with { MaxUploadBytes = 4 });

        Assert.HasCount(2, RtspOutput.Heads(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media" }, Entries(fileSystem));
        Assert.AreEqual($"RTSP session {SessionId} ended: the recording grew past the upload limit of 4 bytes", log.Notes[1]);
        Assert.AreEqual("Recording of /rec.bin discarded: the recording grew past the upload limit of 4 bytes", log.Notes[2]);
    }

    [TestMethod]
    public async Task Record_ConnectionClosedBeforeTeardown_DiscardsThePartialFile()
    {
        var fileSystem = UploadFileSystem();
        var requests = Concat(Ascii(SetupRecord + Record), RtpFrame(0, "abc"));

        var (_, log) = await ServeAsync([requests], TestContext.CancellationToken, fileSystem: fileSystem, exposureOptions: UploadsAllowed);

        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media" }, Entries(fileSystem));
        Assert.AreEqual("Recording of /rec.bin discarded: connection closed", log.Notes[^1]);
    }

    [TestMethod]
    [DataRow(new byte[] { (byte)'$', 0 })]
    [DataRow(new byte[] { (byte)'$', 0, 0, 16, 0x80, 96 })]
    public async Task Record_ClientClosingInTheMiddleOfAFrame_EndsTheConnectionAndDiscardsTheRecording(byte[] partialFrame)
    {
        var fileSystem = UploadFileSystem();

        var (_, log) = await ServeAsync([Concat(Ascii(SetupRecord + Record), partialFrame)], TestContext.CancellationToken, fileSystem: fileSystem, exposureOptions: UploadsAllowed);

        Assert.AreEqual("The client closed the connection in the middle of an interleaved frame.", log.Notes[1]);
        Assert.AreEqual("Recording of /rec.bin discarded: connection closed", log.Notes[^1]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media" }, Entries(fileSystem));
    }

    [TestMethod]
    public async Task Frame_WithNoSessionHeld_IsDiscardedAndTheNextRequestAnswered()
    {
        var (connection, _) = await ServeAsync([Concat(Ascii(NextOptions), RtpFrame(0, "ab"), Ascii(NextOptions))], TestContext.CancellationToken);

        Assert.HasCount(2, RtspOutput.Heads(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task Frame_FirstOnTheConnection_IsABadHeadAndCloses()
    {
        var (connection, _) = await ServeAsync([Concat(RtpFrame(0, "ab"), Ascii("\r\n\r\n"))], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead("400 Bad Request", null), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task Record_PausedForTheSessionTimeout_IsDiscardedWithTheSession()
    {
        var fileSystem = UploadFileSystem();
        var clock = new ManualTimeProvider(Now);
        var released = false;
        var connection = new ScriptedConnection(
            new ScriptedConnection.Chunk(Concat(Ascii(SetupRecord + Record), RtpFrame(0, "abc"), Ascii($"PAUSE * RTSP/1.0\r\nCSeq: 4\r\n{SessionField}\r\n\r\n")), _ => true),
            ScriptedConnection.When(Teardown, _ => released));
        var log = new RecordingExchangeLog();
        var serving = Server(fileSystem, exposureOptions: UploadsAllowed).ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        await WaitForHeadsAsync(connection, 3);
        clock.Advance(RtspSession.Timeout);
        released = true;
        connection.Recheck();
        await serving;

        Assert.StartsWith("RTSP/1.0 454 Session Not Found\r\nCSeq: 3\r\n", RtspOutput.Heads(connection.WrittenBytes)[3]);
        Assert.AreEqual($"RTSP session {SessionId} ended: timeout", log.Notes[1]);
        Assert.AreEqual("Recording of /rec.bin discarded: timeout", log.Notes[2]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media" }, Entries(fileSystem));
    }

    [TestMethod]
    public async Task Record_Recording_OutlivesTheSessionTimeoutAndIsStoredByTeardown()
    {
        var fileSystem = UploadFileSystem();
        var clock = new ManualTimeProvider(Now);
        var released = false;
        var connection = new ScriptedConnection(
            new ScriptedConnection.Chunk(Concat(Ascii(SetupRecord + Record), RtpFrame(0, "abc")), _ => true),
            ScriptedConnection.When(Teardown, _ => released));
        var serving = Server(fileSystem, exposureOptions: UploadsAllowed).ServeAsync(connection, Context(new RecordingExchangeLog(), clock, TestContext.CancellationToken));

        await WaitForHeadsAsync(connection, 2);
        clock.Advance(RtspSession.Timeout * 2);
        released = true;
        connection.Recheck();
        await serving;

        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 3\r\n", RtspOutput.Heads(connection.WrittenBytes)[2]);
        CollectionAssert.AreEqual(Ascii("abc"), StoredBytes(fileSystem, "rec.bin"));
    }

    [TestMethod]
    public async Task Teardown_OfARecordingWhoseLocationBecameADirectory_Is403AndStoresNothing()
    {
        var fileSystem = UploadFileSystem();
        var connection = new ScriptedConnection(
            new ScriptedConnection.Chunk(Concat(Ascii(SetupRecord + Record), RtpFrame(0, "abc")), _ => true),
            ScriptedConnection.When(Teardown, written =>
            {
                var recording = RtspOutput.Heads(written).Count == 2;
                if (recording)
                {
                    fileSystem.CreateDirectory(Path.Join(Root, "rec.bin"));
                }

                return recording;
            }));
        var log = new RecordingExchangeLog();

        await Server(fileSystem, exposureOptions: UploadsAllowed).ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken));

        Assert.AreEqual(ResponseHead("403 Forbidden", "3"), RtspOutput.Heads(connection.WrittenBytes)[2]);
        Assert.AreEqual("Recording of /rec.bin discarded: its location can no longer take a file", log.Notes[1]);
        Assert.AreEqual("RTSP TEARDOWN refused: 403 Forbidden: the recording's location can no longer take a file", log.Notes[3]);
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(Path.Join(Root, "rec.bin")));
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "media", "rec.bin" }, Entries(fileSystem));
    }

    // The served root holding clip.bin and the empty directory media, in a file system that takes uploads.
    private static InMemoryContentFileSystem UploadFileSystem()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        fileSystem.CreateDirectory(Path.Join(Root, "media"));
        WriteFile(fileSystem, "clip.bin", ClipBody);

        return fileSystem;
    }

    private static void WriteFile(InMemoryContentFileSystem fileSystem, string name, string contents)
    {
        using var file = fileSystem.CreateFileForAsyncWrite(Path.Join(Root, name));
        file.Write(Ascii(contents));
    }

    private static byte[] StoredBytes(InMemoryContentFileSystem fileSystem, string name)
    {
        using var file = fileSystem.OpenFileForAsyncRead(Path.Join(Root, name));
        using var copy = new MemoryStream();
        file.CopyTo(copy);

        return copy.ToArray();
    }

    private static string[] Entries(InMemoryContentFileSystem fileSystem) => fileSystem.EnumerateDirectoryEntryNames(Root).ToArray();

    private static async Task WaitForHeadsAsync(ScriptedConnection connection, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (RtspOutput.Heads(connection.WrittenBytes).Count < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }
    }

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    private static byte[] Frame(byte channel, byte[] packet)
    {
        var frame = new byte[RtspInterleavedFrame.FrameHeaderBytes + packet.Length];
        frame[0] = RtspInterleavedFrame.Marker;
        frame[1] = channel;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), (ushort)packet.Length);
        packet.CopyTo(frame, RtspInterleavedFrame.FrameHeaderBytes);

        return frame;
    }

    // An RTP version 2 packet carrying payload, with csrcCount CSRCs, a header extension of
    // extensionWords words when that is not 0, and padding bytes when that is not 0.
    private static byte[] RtpFrame(byte channel, string payload, int csrcCount = 0, int extensionWords = 0, int padding = 0)
    {
        var packet = new List<byte>
        {
            (byte)(0x80 | (padding > 0 ? 0x20 : 0) | (extensionWords > 0 ? 0x10 : 0) | csrcCount),
            96,
        };
        packet.AddRange(new byte[10 + (4 * csrcCount)]);
        if (extensionWords > 0)
        {
            packet.AddRange(new byte[] { 0xBE, 0xDE, 0, (byte)extensionWords });
            packet.AddRange(Enumerable.Repeat((byte)0xEE, 4 * extensionWords));
        }

        packet.AddRange(Ascii(payload));
        if (padding > 0)
        {
            packet.AddRange(new byte[padding - 1]);
            packet.Add((byte)padding);
        }

        return Frame(channel, [.. packet]);
    }
}
