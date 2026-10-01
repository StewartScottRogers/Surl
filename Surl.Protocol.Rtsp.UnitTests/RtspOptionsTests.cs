using Surl.Protocol.Abstractions;
using static Surl.Protocol.Rtsp.RtspServerHarness;

namespace Surl.Protocol.Rtsp;

[TestClass]
public sealed class RtspOptionsTests
{
    private const string PublicField = "Public: OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("options-plain")]
    [DataRow("options-fields")]
    public async Task RecordedOptions_IsAnsweredWithTheBytesUpstreamCurlAccepted(string caseName)
    {
        var (connection, log) = await ServeAsync([RecordedRequest(caseName)], TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedResponse(caseName), connection.WrittenBytes);
        Assert.IsFalse(connection.Aborted);
        CollectionAssert.AreEqual(new[] { "The client closed the connection: ConnectionClosed." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task RecordedOptions_OneBytePerRead_IsAnsweredTheSame()
    {
        var (connection, _) = await ServeAsync(OneBytePerRead(RecordedRequest("options-plain")), TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedResponse("options-plain"), connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("000123456")]
    [DataRow("999999999")]
    public async Task Options_EchoesTheCSeqByteForByte(string cseq)
    {
        var (connection, _) = await ServeAsync([Ascii($"OPTIONS * RTSP/1.0\r\nCSeq: {cseq}\r\n\r\n")], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead("200 OK", cseq, PublicField), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task Options_ReadsTheCSeqFieldNameInAnyCase()
    {
        var (connection, _) = await ServeAsync([Ascii("OPTIONS * RTSP/1.0\r\ncseq: 4\r\n\r\n")], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead("200 OK", "4", PublicField), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("rtsp://127.0.0.1:18554/missing")]
    [DataRow("RTSP://127.0.0.1:18554/clip.bin")]
    [DataRow("rtsp://127.0.0.1:18554/%2e%2e/x")]
    public async Task Options_OfAnyRtspUrl_Is200WithoutALookUp(string requestUri)
    {
        var (connection, _) = await ServeAsync([Ascii($"OPTIONS {requestUri} RTSP/1.0\r\nCSeq: 2\r\n\r\n")], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead("200 OK", "2", PublicField), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task SeveralRequestsOnOneConnection_AreEachAnsweredInOrder()
    {
        var requests = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n"
            + "DESCRIBE * RTSP/1.0\r\nCSeq: 2\r\n\r\n"
            + "PLAY * RTSP/1.0\r\nCSeq: 3\r\n\r\n"
            + "OPTIONS * RTSP/1.0\r\nCSeq: 4\r\n\r\n";

        var (connection, log) = await ServeAsync([Ascii(requests)], TestContext.CancellationToken);

        var expected = ResponseHead("200 OK", "1", PublicField)
            + ResponseHead("400 Bad Request", "2")
            + ResponseHead("454 Session Not Found", "3")
            + ResponseHead("200 OK", "4", PublicField);
        Assert.AreEqual(expected, Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted, "No response closed the connection; the client did.");
        CollectionAssert.AreEqual(
            new[]
            {
                "RTSP DESCRIBE refused: 400 Bad Request: * names no presentation",
                "RTSP PLAY refused: 454 Session Not Found: the request names no session",
                "The client closed the connection: ConnectionClosed.",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task OptionsWithABody_ReadsTheBodyAndAnswersTheNextRequest()
    {
        var requests = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 5\r\n\r\nhello"
            + "OPTIONS * RTSP/1.0\r\nCSeq: 2\r\n\r\n";

        var (connection, _) = await ServeAsync(OneBytePerRead(Ascii(requests)), TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead("200 OK", "1", PublicField) + ResponseHead("200 OK", "2", PublicField), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task BodyWithinAnUploadLimitOfZero_IsReadBecauseZeroMeansNoLimit()
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 0 };

        var (connection, _) = await ServeAsync([Ascii("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 3\r\n\r\nabc")], TestContext.CancellationToken, limits);

        Assert.AreEqual(ResponseHead("200 OK", "1", PublicField), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ExchangeCancelledWhileWaitingForTheNextRequest_EndsWithNoMoreBytes()
    {
        var connection = new InMemoryConnection([RecordedRequest("options-plain")], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var clock = new ManualTimeProvider(Now);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, exchange.Token));

        CollectionAssert.AreEqual(RecordedResponse("options-plain"), connection.WrittenBytes, "The first request was answered.");
        Assert.AreEqual(0, clock.ActiveTimerCount, "No head timer runs while the connection waits for the next head.");
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        CollectionAssert.AreEqual(RecordedResponse("options-plain"), connection.WrittenBytes, "The idle timeout closes with no bytes.");
        Assert.IsFalse(connection.WritesCompleted);
    }
}
