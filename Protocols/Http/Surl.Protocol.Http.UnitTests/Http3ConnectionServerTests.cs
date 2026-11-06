using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// Drives <see cref="Http3ConnectionServer"/> over the in-memory QUIC test connection
/// (<see cref="Http3TestClient"/>): a QUIC connection opened with pinned upstream curl's recorded
/// Initials, then HTTP/3 frames written from RFC 9114 and RFC 9204.
/// </summary>
[TestClass]
public sealed partial class Http3ConnectionServerTests
{
    private const ulong H3NoError = 0x100;
    private const ulong H3ClosedCriticalStream = 0x104;
    private const ulong H3FrameUnexpected = 0x105;
    private const ulong H3MessageError = 0x10e;

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task ServeAsync_GetOfAContentStoreFile_AnswersWhatHttp11AnswersForTheSameFile()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        var (http11Fields, http11Body) = Http11Answer(RecordedResponse("get-file"));
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(http11Fields, response.RegularFields);
        Assert.AreEqual(http11Body, response.BodyText);
        Assert.IsTrue(response.Fin);
        Assert.IsNull(client.StopSendingCode(0), "The request was read to its end.");
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_Handshake_OpensTheControlStreamWithSettingsAndBothQpackStreams()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        await client.UntilAsync(() => client.StreamSent(11).Data.Length == 1, Token);

        CollectionAssert.AreEqual(
            new byte[] { 0x00, 0x04, 0x05, 0x06, 0x80, 0x10, 0x00, 0x00 },
            client.StreamSent(Http3TestClient.ServerControlStreamId).Data,
            "The control stream type, then SETTINGS with SETTINGS_MAX_FIELD_SECTION_SIZE 1048576.");
        CollectionAssert.AreEqual(new byte[] { 0x02 }, client.StreamSent(7).Data);
        CollectionAssert.AreEqual(new byte[] { 0x03 }, client.StreamSent(11).Data);
    }

    [TestMethod]
    public async Task ServeAsync_TwoConcurrentRequestStreams_AnswersEachIndependently()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"));
        client.Send(4, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(4), Token);
        var streamZeroEndedFirst = client.HasEnded(0);
        client.Send(0, [], fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.IsFalse(streamZeroEndedFirst, "Stream 0 waits for its FIN while stream 4 is answered.");
        Assert.AreEqual(FileBody, client.Response(4).BodyText);
        Assert.AreEqual(FileBody, client.Response(0).BodyText);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_PostBodyUnderAllowUploads_AnswersWhatHttp11AnswersAndStopsTheUnreadBody()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, allowUploads: true);

        client.Send(0, [.. client.Request("POST", "/upload.txt", ("content-length", "5")), .. Http3TestClient.Frame(Http3FrameType.Data, Ascii("hello"))], fin: true);
        await client.UntilAsync(() => client.HasEnded(0) && client.StopSendingCode(0) is not null, Token);

        var response = client.Response(0);
        Assert.AreEqual("405", response.Status);
        CollectionAssert.Contains(response.RegularFields, "allow: GET, HEAD, PUT");
        Assert.IsTrue(response.Fin);
        Assert.AreEqual(H3NoError, client.StopSendingCode(0), "RFC 9114 section 4.1: the body left unread is stopped H3_NO_ERROR.");
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_HeadersFrameLongerThanMaxMessage_Answers431ThenStopsAndTheConnectionCarriesOn()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { MaxMessageBytes = 200 });

        client.Send(0, client.Request("GET", "/file.txt", ("x-long", new string('a', 300))), fin: true);
        client.Send(4, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0) && client.HasEnded(4) && client.StopSendingCode(0) is not null, Token);

        var refused = client.Response(0);
        Assert.AreEqual("431", refused.Status);
        Assert.IsTrue(refused.Fin);
        Assert.AreEqual(H3NoError, client.StopSendingCode(0));
        Assert.AreEqual(FileBody, client.Response(4).BodyText);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    [DataRow("missing :scheme")]
    [DataRow("connection-specific field")]
    public async Task ServeAsync_MalformedRequest_ResetsAndStopsTheStreamWithMessageError(string malformation)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);
        var headers = malformation == "missing :scheme"
            ? client.Headers((":method", "GET"), (":authority", "127.0.0.1:18050"), (":path", "/file.txt"))
            : client.Request("GET", "/file.txt", ("connection", "close"));

        client.Send(0, headers, fin: true);
        await client.UntilAsync(() => client.ResetCode(0) is not null && client.StopSendingCode(0) is not null, Token);

        Assert.AreEqual(H3MessageError, client.ResetCode(0));
        Assert.AreEqual(H3MessageError, client.StopSendingCode(0));
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_SecondSettingsOnTheControlStream_ClosesWithFrameUnexpected()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(Http3TestClient.ControlStreamId, Http3TestClient.Frame(Http3FrameType.Settings, []));
        await client.FinishAsync(Token);

        Assert.AreEqual(H3FrameUnexpected, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesItsControlStream_ClosesWithClosedCriticalStream()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(Http3TestClient.ControlStreamId, [], fin: true);
        await client.FinishAsync(Token);

        Assert.AreEqual(H3ClosedCriticalStream, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledAtShutdown_SendsGoAwayNamingTheNextStreamThenClosesWithNoError()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);
        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        await client.Exchange.CancelAsync();
        await client.FinishAsync(Token);

        var control = Http3TestClient.ReadFrames(client.StreamSent(Http3TestClient.ServerControlStreamId).Data.AsSpan(1));
        Assert.AreEqual((Http3FrameType.GoAway, (byte)0x04), (control[^1].Type, control[^1].Payload.Single()));
        Assert.AreEqual(H3NoError, client.CloseCode);
        Assert.IsTrue(client.Log.Notes.Contains("The exchange was cancelled; sent GOAWAY 4 and closed the HTTP/3 connection with H3_NO_ERROR."));
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
}
