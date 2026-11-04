using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ws.ClientFrames;
using static Surl.Protocol.Ws.WsServerHarness;

namespace Surl.Protocol.Ws;

/// <summary>
/// What the server sends once upgraded, and how it answers each client frame (ADR-0071
/// decisions 4 to 6), over <see cref="InMemoryConnection"/>.
/// </summary>
[TestClass]
public sealed class WsExchangeTests
{
    private const string EmptyClose = "\x88\x00";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task File_IsSentAsOneBinaryMessage_ThenAnEmptyClose()
    {
        var (connection, log) = await ServeFramesAsync(false, TestContext.CancellationToken);

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(
            new[] { "WebSocket upgrade accepted for /chat", "The client closed the connection without a CLOSE.", "Sent 4 bytes of /chat as a binary message in 1 frames" },
            log.Notes.Take(3).ToArray());
    }

    [TestMethod]
    public async Task File_Over65536Bytes_IsSentInFramesOf65536_TheFirstBinaryTheRestContinuations()
    {
        var contents = Rfc6455Examples.CountingPayload(200000);

        var (connection, log) = await ServeFramesAsync(false, TestContext.CancellationToken, fileSystem: FileSystemWithChat(contents));

        var expected = Rfc6455Examples.Join(
            Latin1Bytes(Recorded101Head),
            [0x02, 0x7F, 0, 0, 0, 0, 0, 1, 0, 0], contents[..65536],
            [0x00, 0x7F, 0, 0, 0, 0, 0, 1, 0, 0], contents[65536..131072],
            [0x00, 0x7F, 0, 0, 0, 0, 0, 1, 0, 0], contents[131072..196608],
            [0x80, 0x7E, 0x0D, 0x40], contents[196608..],
            [0x88, 0x00]);
        CollectionAssert.AreEqual(expected, connection.WrittenBytes);
        Assert.Contains("Sent 200000 bytes of /chat as a binary message in 4 frames", log.Notes);
    }

    [TestMethod]
    public async Task File_ofExactly65536Bytes_IsOneFrame()
    {
        var contents = Rfc6455Examples.CountingPayload(65536);

        var (connection, _) = await ServeFramesAsync(false, TestContext.CancellationToken, fileSystem: FileSystemWithChat(contents));

        var expected = Rfc6455Examples.Join(Latin1Bytes(Recorded101Head), [0x82, 0x7F, 0, 0, 0, 0, 0, 1, 0, 0], contents, [0x88, 0x00]);
        CollectionAssert.AreEqual(expected, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task EmptyFile_IsOneEmptyBinaryFrame()
    {
        var (connection, log) = await ServeFramesAsync(false, TestContext.CancellationToken, fileSystem: FileSystemWithChat([]));

        Assert.AreEqual(Recorded101Head + "\x82\x00" + EmptyClose, Latin1(connection.WrittenBytes));
        Assert.Contains("Sent 0 bytes of /chat as a binary message in 1 frames", log.Notes);
    }

    [TestMethod]
    public async Task Directory_WithListingsOn_IsSentAsOneTextMessage_EachNameThenLf_ADirectoryWithASlash()
    {
        var fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(Path.Join(Root, "sub"))
            .AddFile(Path.Join(Root, "sub", "a.txt"), Encoding.ASCII.GetBytes("a"), Now)
            .AddDirectory(Path.Join(Root, "sub", "inner"));
        var connection = new InMemoryConnection([RecordedUpgradeRequestWith("GET /chat ", "GET /sub/ ")]);
        var log = new RecordingExchangeLog();

        await WithinTimeout(Server(fileSystem, echoesMessages: false, listDirectories: true).ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken)));

        Assert.AreEqual(Recorded101Head + "\u0081\u000Da.txt\ninner/\n" + EmptyClose, Latin1(connection.WrittenBytes));
        Assert.Contains("Sent 13 bytes of /sub/ as a text message in 1 frames", log.Notes);
    }

    [TestMethod]
    public async Task FileThatCannotBeRead_IsAnsweredClose1011()
    {
        var fileSystem = StandardFileSystem().FailOn(Path.Join(Root, "chat"), "OpenFileForAsyncRead", new IOException("disk failed"));

        var (connection, log) = await ServeFramesAsync(false, TestContext.CancellationToken, fileSystem: fileSystem);

        Assert.AreEqual(Recorded101Head + "\x88\x02\x03\xF3", Latin1(connection.WrittenBytes));
        Assert.Contains("Closing with 1011: /chat could not be read (disk failed)", log.Notes);
    }

    [TestMethod]
    public async Task Ping_IsAnsweredWithAPongCarryingTheSamePayload_BeforeTheNextDataFrame()
    {
        var (connection, log) = await ServeFramesAsync(false, TestContext.CancellationToken, clientFrames: Masked(0x89, "Hello"));

        Assert.AreEqual(Recorded101Head + "\x8A\x05Hello" + "\u0082\u0004chat" + EmptyClose, Latin1(connection.WrittenBytes));
        Assert.AreEqual("Answered PING with PONG, 5 bytes", log.Notes[1]);
    }

    [TestMethod]
    public async Task RecordedPongOfUpstreamCurl_IsIgnored()
    {
        // Fixtures/ping-pong: curl 8.21.0 answered "89 04 ping" with a masked PONG carrying "ping".
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("ping-pong")]);
        var log = new RecordingExchangeLog();

        await WithinTimeout(Server().ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken)));

        var expected = Recorded101Head.Replace("ktpdlwK4CWD8HWwKyLX0kug7bZ8=", "gUJdUVfWcfIKTSK+89U8hTGPDx8=", StringComparison.Ordinal) + "\u0082\u0004chat" + EmptyClose;
        Assert.AreEqual(expected, Latin1(connection.WrittenBytes));
        Assert.IsFalse(log.Notes.Any(note => note.StartsWith("Closing with", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ClientClose_BeforeAnyDataFrame_StopsTheFile_AndIsEchoedWithItsCodeAlone()
    {
        var (connection, log) = await ServeFramesAsync(false, TestContext.CancellationToken, clientFrames: Close(1000, "bye"));

        Assert.AreEqual(Recorded101Head + "\x88\x02\x03\xE8", Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { "Client closed with 1000", "Sent 0 bytes of /chat as a binary message in 0 frames" }, log.Notes.Skip(1).Take(2).ToArray());
    }

    [TestMethod]
    public async Task EmptyClientClose_IsEchoedEmpty()
    {
        var (connection, log) = await ServeFramesAsync(true, TestContext.CancellationToken, clientFrames: Masked(0x88, ""));

        Assert.AreEqual(Recorded101Head + EmptyClose, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { "WebSocket upgrade accepted for /chat, echoing", "Client closed with no code" }, log.Notes.Take(2).ToArray());
    }

    [TestMethod]
    public async Task Echo_SendsEachMessageBackWithItsOpcode_UntilTheClientCloses()
    {
        var (connection, _) = await ServeFramesAsync(
            true, TestContext.CancellationToken, clientFrames: [Masked(0x81, "hi"), Masked(0x82, "\x00\x01"), Close(1001)]);

        Assert.AreEqual(Recorded101Head + "\x81\x02hi" + "\x82\x02\x00\x01" + "\x88\x02\x03\xE9", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task FragmentedMessage_IsReassembled_WithAPingBetweenItsFragmentsAnsweredInPlace()
    {
        var (connection, _) = await ServeFramesAsync(
            true, TestContext.CancellationToken, clientFrames: [Masked(0x01, "Hel"), Masked(0x89, "p"), Masked(0x80, "lo"), Close(1000)]);

        Assert.AreEqual(Recorded101Head + "\x8A\x01p" + "\x81\x05Hello" + "\x88\x02\x03\xE8", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task EchoOfAMessageOver65536Bytes_IsSentInFramesOf65536()
    {
        var payload = Rfc6455Examples.CountingPayload(70000);

        var (connection, _) = await ServeFramesAsync(true, TestContext.CancellationToken, clientFrames: [Masked(0x82, payload), Close(1000)]);

        var expected = Rfc6455Examples.Join(
            Latin1Bytes(Recorded101Head),
            [0x02, 0x7F, 0, 0, 0, 0, 0, 1, 0, 0], payload[..65536],
            [0x80, 0x7E, 0x11, 0x70], payload[65536..],
            [0x88, 0x02, 0x03, 0xE8]);
        CollectionAssert.AreEqual(expected, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task MaxMessageOfZero_SetsNoLimit()
    {
        var payload = Rfc6455Examples.CountingPayload(300);
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 0 };

        var (connection, _) = await ServeFramesAsync(true, TestContext.CancellationToken, limits, clientFrames: [Masked(0x82, payload), Close(1000)]);

        CollectionAssert.AreEqual(
            Rfc6455Examples.Join(Latin1Bytes(Recorded101Head), [0x82, 0x7E, 0x01, 0x2C], payload, [0x88, 0x02, 0x03, 0xE8]),
            connection.WrittenBytes);
    }

    [TestMethod]
    public async Task DataMessage_WithoutEcho_IsReadAndDiscarded()
    {
        var (connection, log) = await ServeFramesAsync(false, TestContext.CancellationToken, clientFrames: Masked(0x81, "ignored"));

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.IsFalse(log.Notes.Any(note => note.StartsWith("Closing with", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ClientThatClosesWithoutAClose_IsNotSentOne_WhenEchoing()
    {
        var (connection, log) = await ServeFramesAsync(true, TestContext.CancellationToken, clientFrames: Masked(0x81, "hi"));

        Assert.AreEqual(Recorded101Head + "\x81\x02hi", Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("The client closed the connection without a CLOSE.", log.Notes[1]);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x81, 0x02, 0x68, 0x69 }, "\x03\xEA", "a client frame is not masked", DisplayName = "unmasked frame: 1002")]
    [DataRow(new byte[] { 0xC1, 0x82, 1, 2, 3, 4, 0x69, 0x6B }, "\x03\xEA", "a client frame sets a reserved bit", DisplayName = "RSV1: 1002")]
    [DataRow(new byte[] { 0xA1, 0x80, 1, 2, 3, 4 }, "\x03\xEA", "a client frame sets a reserved bit", DisplayName = "RSV2: 1002")]
    [DataRow(new byte[] { 0x91, 0x80, 1, 2, 3, 4 }, "\x03\xEA", "a client frame sets a reserved bit", DisplayName = "RSV3: 1002")]
    [DataRow(new byte[] { 0x83, 0x80, 1, 2, 3, 4 }, "\x03\xEA", "a client frame has a reserved opcode", DisplayName = "reserved opcode: 1002")]
    [DataRow(new byte[] { 0x89, 0xFE, 0x00, 0x7E }, "\x03\xEA", "a client control frame's payload is over 125 bytes", DisplayName = "oversized control frame: 1002")]
    [DataRow(new byte[] { 0x09, 0x80, 1, 2, 3, 4 }, "\x03\xEA", "a client control frame is fragmented", DisplayName = "fragmented control frame: 1002")]
    [DataRow(new byte[] { 0x80, 0x80, 1, 2, 3, 4 }, "\x03\xEA", "a client continuation frame arrived with no message started", DisplayName = "continuation first: 1002")]
    [DataRow(new byte[] { 0x82, 0xFE, 0x00, 0x05 }, "\x03\xEA", "a client frame's payload length is not in its shortest form", DisplayName = "non-minimal length: 1002")]
    [DataRow(new byte[] { 0x82, 0xFF, 0x80, 0, 0, 0, 0, 0, 0, 0 }, "\x03\xEA", "a client frame's 64-bit payload length has its top bit set", DisplayName = "64-bit top bit: 1002")]
    [DataRow(new byte[] { 0x88, 0x81, 1, 2, 3, 4, 0x02 }, "\x03\xEA", "a client CLOSE's payload is one byte", DisplayName = "one-byte close: 1002")]
    [DataRow(new byte[] { 0x88, 0x82, 0, 0, 0, 0, 0x03, 0xED }, "\x03\xEA", "a client CLOSE carries a code not allowed on the wire", DisplayName = "close 1005: 1002")]
    [DataRow(new byte[] { 0x88, 0x83, 0, 0, 0, 0, 0x03, 0xE8, 0xFF }, "\x03\xEF", "a client CLOSE's reason is not valid UTF-8", DisplayName = "close reason not UTF-8: 1007")]
    [DataRow(new byte[] { 0x81, 0x82, 0, 0, 0, 0, 0xFF, 0xFE }, "\x03\xEF", "a client text message is not valid UTF-8", DisplayName = "text not UTF-8: 1007")]
    [DataRow(new byte[] { 0x82, 0x85, 0, 0 }, "\x03\xEA", "the client closed the connection partway through a frame", DisplayName = "cut short: 1002")]
    public async Task InvalidClientFrame_IsClosedWithItsCode(byte[] clientFrame, string closeCode, string why)
    {
        var (connection, log) = await ServeFramesAsync(true, TestContext.CancellationToken, clientFrames: clientFrame);

        Assert.AreEqual(Recorded101Head + "\x88\x02" + closeCode, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual($"Closing with {(closeCode[0] << 8) | closeCode[1]}: {why}", log.Notes[1]);
    }

    [TestMethod]
    public async Task DataFrame_InsideAnUnfinishedMessage_IsClosedWith1002()
    {
        var (connection, log) = await ServeFramesAsync(true, TestContext.CancellationToken, clientFrames: [Masked(0x01, "a"), Masked(0x81, "b")]);

        Assert.AreEqual(Recorded101Head + "\x88\x02\x03\xEA", Latin1(connection.WrittenBytes));
        Assert.AreEqual("Closing with 1002: a client data frame arrived inside an unfinished message", log.Notes[1]);
    }

    [TestMethod]
    public async Task Frame_PastMaxMessage_IsClosedWith1009_BeforeItsPayloadIsRead()
    {
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 100 };

        var (connection, log) = await ServeFramesAsync(true, TestContext.CancellationToken, limits, clientFrames: new byte[] { 0x82, 0xFF, 0, 0, 0, 0, 0, 0x20, 0, 0, 1, 2, 3, 4 });

        Assert.AreEqual(Recorded101Head + "\x88\x02\x03\xF1", Latin1(connection.WrittenBytes));
        Assert.AreEqual("Closing with 1009: a client frame is past --max-message 100", log.Notes[1]);
    }

    [TestMethod]
    public async Task ReassembledMessage_PastMaxMessage_IsClosedWith1009()
    {
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 100 };
        var sixty = new string('x', 60);

        var (connection, log) = await ServeFramesAsync(true, TestContext.CancellationToken, limits, clientFrames: [Masked(0x01, sixty), Masked(0x80, sixty)]);

        Assert.AreEqual(Recorded101Head + "\x88\x02\x03\xF1", Latin1(connection.WrittenBytes));
        Assert.AreEqual("Closing with 1009: a client message is past --max-message 100", log.Notes[1]);
    }

    private static UnitTestInMemoryContentFileSystem FileSystemWithChat(byte[] contents) => new UnitTestInMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Join(Root, "chat"), contents, Now);
}
