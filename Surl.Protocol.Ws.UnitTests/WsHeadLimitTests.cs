using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ws.WsServerHarness;

namespace Surl.Protocol.Ws;

/// <summary>
/// ADR-0071 decision 1, check 1: a head that cannot be read within <c>--max-request-head</c>
/// and the head timeout, or does not parse, is answered as the HTTP server answers it.
/// </summary>
[TestClass]
public sealed class WsHeadLimitTests
{
    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task HeadPastMaxRequestHead_IsAnswered431()
    {
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 64 };

        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("upgrade-101")], TestContext.CancellationToken, limits: limits);

        Assert.AreEqual(Refusal("431 Request Header Fields Too Large"), Latin1(connection.WrittenBytes));
        Assert.AreEqual("WebSocket upgrade refused: 431 Request Header Fields Too Large: no request head was read (HeadTooLarge); closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task RecordedHead_AtExactlyMaxRequestHead_IsUpgraded()
    {
        var request = RecordedFixture.ReadRequestBytes("upgrade-101");
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = request.Length };

        var (connection, _) = await ServeAsync([request], TestContext.CancellationToken, limits: limits);

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("GET /chat HTTP/1.1\r\n", "GET /chat HTTP/2.0\r\n", "505 HTTP Version Not Supported", "UnsupportedVersion", DisplayName = "HTTP/2.0")]
    [DataRow("GET /chat HTTP/1.1\r\n", "GET /chat\r\n", "400 Bad Request", "MalformedRequestLine", DisplayName = "no version")]
    [DataRow("Accept: */*\r\n", "Accept : */*\r\n", "400 Bad Request", "WhitespaceBeforeColon", DisplayName = "space before colon")]
    public async Task HeadThatDoesNotParse_IsAnsweredAsTheHttpServerAnswersIt(string oldText, string newText, string statusLine, string outcome)
    {
        var (connection, log) = await ServeAsync([RecordedUpgradeRequestWith(oldText, newText)], TestContext.CancellationToken);

        Assert.AreEqual(Refusal(statusLine), Latin1(connection.WrittenBytes));
        Assert.AreEqual($"WebSocket upgrade refused: {statusLine}: no request head was read ({outcome}); closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task PartialHead_AfterHeadTimeout_IsAnswered408()
    {
        var clock = new ManualTimeProvider(Now - HeadTimeout);
        var connection = new InMemoryConnection([Latin1Bytes("GET /chat HTTP/1.1\r\nHost:")], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        clock.Advance(HeadTimeout);
        await EndLingeringCloseAsync(clock, connection, serving);

        Assert.AreEqual(Refusal("408 Request Timeout"), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(
            new[]
            {
                "WebSocket upgrade refused: 408 Request Timeout: no request head was read (HeadTimedOut); closed.",
                "Stopped reading what the client still sent at the 1-second lingering close.",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task NoByte_BeforeHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        clock.Advance(HeadTimeout);
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "No request head was read: HeadTimedOutBeforeAnyByte; closed with no bytes." }, log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow("", "ConnectionClosed", DisplayName = "before any byte")]
    [DataRow("GET /chat HTTP/1.1\r\nHost: a\r\n", "ConnectionClosedBeforeHeadEnded", DisplayName = "part way through")]
    public async Task ClientCloses_BeforeTheHeadEnds_GetsNoBytes(string sent, string outcome)
    {
        var (connection, log) = await ServeAsync([Latin1Bytes(sent)], TestContext.CancellationToken);

        Assert.IsEmpty(connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { $"The client closed the connection: {outcome}." }, log.Notes.ToArray());
    }
}
