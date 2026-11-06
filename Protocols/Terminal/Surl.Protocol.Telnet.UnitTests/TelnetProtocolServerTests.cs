using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Telnet;

[TestClass]
public sealed class TelnetProtocolServerTests
{
    private const byte Iac = 255;
    private const byte Will = 251;
    private const byte Wont = 252;
    private const byte Do = 253;
    private const byte Dont = 254;
    private const byte Sb = 250;
    private const byte Se = 240;

    private static readonly byte[] Opening =
    [
        Iac, Will, 3, Iac, Do, 24, Iac, Do, 35, Iac, Do, 39, Iac, Do, 31,
        .. "surl TELNET server: each line is echoed back; quit ends the session.\r\n"u8,
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Schemes_IsTelnetOnly()
    {
        CollectionAssert.AreEqual(new[] { "telnet" }, new TelnetProtocolServer().Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new TelnetProtocolServer();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog())));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    [DataRow("plain-session", false)]
    [DataRow("plain-session", true)]
    [DataRow("telnet-options-session", false)]
    [DataRow("telnet-options-session", true)]
    [DataRow("iac-in-data", false)]
    [DataRow("iac-in-data", true)]
    public async Task ServeAsync_RecordedSession_SendsTheBytesUpstreamCurlAccepted(string caseName, bool oneBytePerRead)
    {
        var request = RecordedFixture.ReadRequestBytes(caseName);
        var chunks = oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request);

        var (connection, _) = await ServeChunksAsync(chunks, peerHalfCloses: false);

        Assert.AreEqual("0", Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
        Assert.AreEqual(0, RecordedFixture.ReadBytes(caseName, "stderr.txt").Length);
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes(caseName, "replies.bin"), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedSessionWithTelnetOptions_ReportsTerminalTypeAndEnvironment()
    {
        var (_, log) = await ServeChunksAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("telnet-options-session")), peerHalfCloses: false);

        CollectionAssert.AreEqual(
            new[]
            {
                "The client's window size (NAWS) is 0x0.",
                "The client's terminal type (TERMINAL-TYPE) is vt100.",
                "The client's environment (NEW-ENVIRON) is USER=alice.",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedPlainSession_ReportsOnlyTheWindowSize()
    {
        var (_, log) = await ServeChunksAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("plain-session")), peerHalfCloses: false);

        CollectionAssert.AreEqual(new[] { "The client's window size (NAWS) is 0x0." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_IacIacInData_IsOneByteEchoedDoubled()
    {
        var reply = await ServeAfterOpeningAsync([Iac, Iac, (byte)'x', Iac, Iac, (byte)'\n']);

        CollectionAssert.AreEqual(new byte[] { Iac, Iac, (byte)'x', Iac, Iac, (byte)'\r', (byte)'\n' }, reply);
    }

    [TestMethod]
    [DataRow("a\r\n")]
    [DataRow("a\n")]
    [DataRow("a\r\0")]
    public async Task ServeAsync_LineEndings_EndALineEchoedWithCrLf(string line)
    {
        var reply = await ServeAfterOpeningAsync(Encoding.ASCII.GetBytes(line));

        Assert.AreEqual("a\r\n", Encoding.ASCII.GetString(reply));
    }

    [TestMethod]
    public async Task ServeAsync_NulWithoutCarriageReturn_IsData()
    {
        var reply = await ServeAfterOpeningAsync("\0a\n"u8.ToArray());

        Assert.AreEqual("\0a\r\n", Encoding.ASCII.GetString(reply));
    }

    [TestMethod]
    public async Task ServeAsync_PartLineAtClose_IsNotEchoed()
    {
        var reply = await ServeAfterOpeningAsync("abc"u8.ToArray());

        Assert.AreEqual(0, reply.Length);
    }

    [TestMethod]
    [DataRow("quit")]
    [DataRow("QUIT")]
    public async Task ServeAsync_Quit_AnswersByeAndIgnoresTheRest(string quit)
    {
        var (connection, _) = await ServeAsync([Encoding.ASCII.GetBytes($"one\n{quit}\ntwo\n")], peerHalfCloses: false);

        Assert.AreEqual("one\r\nbye\r\n", Encoding.ASCII.GetString(AfterOpening(connection)));
    }

    [TestMethod]
    [DataRow(Will, (byte)99, Dont)]
    [DataRow(Do, (byte)99, Wont)]
    [DataRow(Do, (byte)24, Wont)]
    [DataRow(Will, (byte)1, Dont)]
    public async Task ServeAsync_UnknownOption_IsRefused(byte verb, byte option, byte refusal)
    {
        var reply = await ServeAfterOpeningAsync([Iac, verb, option]);

        CollectionAssert.AreEqual(new byte[] { Iac, refusal, option }, reply);
    }

    [TestMethod]
    [DataRow(Will, (byte)0, Do)]
    [DataRow(Will, (byte)3, Do)]
    [DataRow(Do, (byte)0, Will)]
    public async Task ServeAsync_SupportedOptionOffered_IsAgreedOnceOnly(byte verb, byte option, byte agreement)
    {
        var reply = await ServeAfterOpeningAsync([Iac, verb, option, Iac, verb, option]);

        CollectionAssert.AreEqual(new byte[] { Iac, agreement, option }, reply);
    }

    [TestMethod]
    public async Task ServeAsync_AnswersToTheOpening_AreNotAnswered()
    {
        var reply = await ServeAfterOpeningAsync([Iac, Do, 3, Iac, Will, 31, Iac, Wont, 35, Iac, Dont, 3]);

        CollectionAssert.AreEqual(new byte[] { Iac, Wont, 3 }, reply);
    }

    [TestMethod]
    public async Task ServeAsync_OptionTurnedOffAfterOn_IsConfirmedOnce()
    {
        var reply = await ServeAfterOpeningAsync([Iac, Will, 0, Iac, Wont, 0, Iac, Wont, 0, Iac, Dont, 0, Iac, Do, 0, Iac, Dont, 0]);

        CollectionAssert.AreEqual(new byte[] { Iac, Do, 0, Iac, Dont, 0, Iac, Will, 0, Iac, Wont, 0 }, reply);
    }

    [TestMethod]
    public async Task ServeAsync_ClientPerformsXDisplayLocation_SendsSendAndReportsTheValue()
    {
        var (connection, log) = await ServeAsync(
            [[Iac, Will, 35, Iac, Will, 35, Iac, Sb, 35, 0, .. "host:0"u8, Iac, Se, .. "quit\n"u8]],
            peerHalfCloses: false);

        CollectionAssert.AreEqual((byte[])[Iac, Sb, 35, 1, Iac, Se, .. "bye\r\n"u8], AfterOpening(connection));
        CollectionAssert.AreEqual(new[] { "The client's X display location (X-DISPLAY-LOCATION) is host:0." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_QuitWhileSendIsUnanswered_WaitsForTheAnswer()
    {
        var (connection, log) = await ServeAsync(
            [[Iac, Will, 24, .. "quit\nignored\n"u8], [Iac, Sb, 24, 0, .. "xterm"u8, Iac, Se]],
            peerHalfCloses: false);

        CollectionAssert.AreEqual((byte[])[Iac, Sb, 24, 1, Iac, Se, .. "bye\r\n"u8], AfterOpening(connection));
        CollectionAssert.AreEqual(new[] { "The client's terminal type (TERMINAL-TYPE) is xterm." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_QuitThenClientWithdrawsTheOption_EndsWithoutTheAnswer()
    {
        var (connection, _) = await ServeAsync([[Iac, Will, 39, .. "quit\n"u8, Iac, Wont, 39]], peerHalfCloses: false);

        CollectionAssert.AreEqual((byte[])[Iac, Sb, 39, 1, Iac, Se, .. "bye\r\n"u8, Iac, Dont, 39], AfterOpening(connection));
    }

    [TestMethod]
    [DataRow(new byte[] { Iac })]
    [DataRow(new byte[] { Iac, Do })]
    [DataRow(new byte[] { Iac, Sb })]
    [DataRow(new byte[] { Iac, Sb, 24, 0, (byte)'v' })]
    [DataRow(new byte[] { Iac, Sb, 24, 0, Iac })]
    public async Task ServeAsync_ClosedPartWayThroughACommand_NotesItAndEnds(byte[] request)
    {
        var (connection, log) = await ServeAsync([request]);

        CollectionAssert.AreEqual(Opening, connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "The client closed the connection part way through a TELNET command." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ClosedBetweenCommands_EndsWithNoNote()
    {
        var (_, log) = await ServeAsync([[Iac, Will, 3]]);

        Assert.AreEqual(0, log.Notes.Count);
    }

    [TestMethod]
    public async Task ServeAsync_OtherCommands_AreIgnored()
    {
        var reply = await ServeAfterOpeningAsync([Iac, 241, Iac, 246, Iac, Se, .. "a\n"u8]);

        Assert.AreEqual("a\r\n", Encoding.ASCII.GetString(reply));
    }

    [TestMethod]
    public async Task ServeAsync_SubnegotiationEndedByAnotherCommand_IsDiscardedAndTheCommandAnswered()
    {
        var (connection, log) = await ServeAsync([[Iac, Will, 24, Iac, Sb, 24, 0, (byte)'v', Iac, Will, 3, .. "a\n"u8]]);

        CollectionAssert.AreEqual((byte[])[Iac, Sb, 24, 1, Iac, Se, Iac, Do, 3, .. "a\r\n"u8], AfterOpening(connection));
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_SubnegotiationNamingNoOption_IsReadAsACommand()
    {
        var reply = await ServeAfterOpeningAsync([Iac, Sb, Iac, Se, .. "a\n"u8]);

        Assert.AreEqual("a\r\n", Encoding.ASCII.GetString(reply));
    }

    [TestMethod]
    public async Task ServeAsync_IacIacInSubnegotiation_IsOneByte()
    {
        var (_, log) = await ServeAsync([[Iac, Will, 31, Iac, Sb, 31, 0, Iac, Iac, 0, 24, Iac, Se]]);

        CollectionAssert.AreEqual(new[] { "The client's window size (NAWS) is 255x24." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_SubnegotiationsNotAwaited_AreNotReported()
    {
        byte[] windowSize = [Iac, Sb, 31, 0, 80, 0, 24, Iac, Se];

        var (connection, log) = await ServeAsync(
            [[Iac, Sb, 24, 0, (byte)'x', Iac, Se, Iac, Will, 31, .. windowSize, .. windowSize, Iac, Wont, 31, Iac, Will, 31, Iac, Wont, 31, .. windowSize]]);

        CollectionAssert.AreEqual((byte[])[Iac, Dont, 31, Iac, Do, 31, Iac, Dont, 31], AfterOpening(connection));
        CollectionAssert.AreEqual(new[] { "The client's window size (NAWS) is 80x24." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_OptionOfferedAfterQuit_IsNotSentSend()
    {
        var (connection, log) = await ServeAsync([[Iac, Will, 39, .. "quit\n"u8, Iac, Will, 24, Iac, Sb, 39, 0, Iac, Se]], peerHalfCloses: false);

        CollectionAssert.AreEqual((byte[])[Iac, Sb, 39, 1, Iac, Se, .. "bye\r\n"u8], AfterOpening(connection));
        CollectionAssert.AreEqual(new[] { "The client's environment (NEW-ENVIRON) is empty." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_LineLongerThanTheLimit_AnswersLineTooLongAndCloses()
    {
        var (connection, log) = await ServeAsync(["abcd\r\nabcdef\nnever\n"u8.ToArray()], maxLineBytes: 6);

        Assert.AreEqual("abcd\r\nline too long\r\n", Encoding.ASCII.GetString(AfterOpening(connection)));
        CollectionAssert.AreEqual(new[] { "A line was longer than 6 bytes; answered line too long and closed." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_SubnegotiationLongerThanTheLimit_ClosesWithNoAnswer()
    {
        var (connection, log) = await ServeAsync([[Iac, Sb, 24, 0, .. "abcdef"u8, Iac, Se, .. "a\n"u8]], maxLineBytes: 4, peerHalfCloses: false);

        CollectionAssert.AreEqual(Opening, connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "A subnegotiation for option 24 was longer than 4 bytes; closed." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NoLineLimit_EchoesALongLine()
    {
        var line = new string('x', 9000);

        var (connection, _) = await ServeAsync([Encoding.ASCII.GetBytes(line + "\n")], maxLineBytes: 0);

        Assert.AreEqual(line + "\r\n", Encoding.ASCII.GetString(AfterOpening(connection)));
    }

    [TestMethod]
    public async Task ServeAsync_NoLineLimit_ReportsALongSubnegotiation()
    {
        var terminalType = new string('t', 9000);

        var (_, log) = await ServeAsync([[Iac, Will, 24, Iac, Sb, 24, 0, .. Encoding.ASCII.GetBytes(terminalType), Iac, Se]], maxLineBytes: 0);

        CollectionAssert.AreEqual(new[] { $"The client's terminal type (TERMINAL-TYPE) is {terminalType}." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_CancelledWhileWaiting_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        var serving = new TelnetProtocolServer().ServeAsync(connection, Context(new RecordingExchangeLog(), cancellationToken: cancellation.Token));

        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => serving);
        CollectionAssert.AreEqual(Opening, connection.WrittenBytes);
    }

    private static byte[] AfterOpening(InMemoryConnection connection)
    {
        var written = connection.WrittenBytes;
        CollectionAssert.AreEqual(Opening, written[..Opening.Length]);

        return written[Opening.Length..];
    }

    private static ExchangeContext Context(IExchangeLog log, long maxLineBytes = 8192, CancellationToken cancellationToken = default) => new(
        1,
        new ListenUrl("telnet", "127.0.0.1", 18623).WithBoundPort(18623),
        new IPEndPoint(IPAddress.Loopback, 18623),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        TimeProvider.System,
        cancellationToken)
    {
        Limits = ExchangeLimits.Default with { MaxLineBytes = maxLineBytes },
    };

    private async Task<byte[]> ServeAfterOpeningAsync(byte[] request)
    {
        var (connection, _) = await ServeAsync([request]);

        return AfterOpening(connection);
    }

    private Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(byte[][] chunks, long maxLineBytes = 8192, bool peerHalfCloses = true) =>
        ServeChunksAsync(chunks.Select(chunk => new ReadOnlyMemory<byte>(chunk)), maxLineBytes, peerHalfCloses);

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeChunksAsync(
        IEnumerable<ReadOnlyMemory<byte>> chunks, long maxLineBytes = 8192, bool peerHalfCloses = true)
    {
        var connection = new InMemoryConnection(chunks, peerHalfClosesWhenExhausted: peerHalfCloses);
        var log = new RecordingExchangeLog();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        await new TelnetProtocolServer().ServeAsync(connection, Context(log, maxLineBytes, timeout.Token));

        return (connection, log);
    }
}
