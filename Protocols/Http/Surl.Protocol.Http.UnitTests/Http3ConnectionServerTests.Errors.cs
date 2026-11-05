using Surl.Protocol.Abstractions;
using Surl.Quic;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>The rules of RFC 9114 sections 6, 7 and 8 that end the connection, and the client's streams the server ignores.</summary>
public sealed partial class Http3ConnectionServerTests
{
    private const ulong H3GeneralProtocolError = 0x101;
    private const ulong H3StreamCreationError = 0x103;
    private const ulong H3FrameError = 0x106;
    private const ulong H3ExcessiveLoad = 0x107;
    private const ulong H3SettingsError = 0x109;
    private const ulong H3MissingSettings = 0x10a;
    private const ulong QpackDecompressionFailed = 0x200;
    private const ulong QpackEncoderStreamError = 0x201;
    private const ulong QpackDecoderStreamError = 0x202;

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x07, 0x01, 0x00 }, H3MissingSettings, DisplayName = "GOAWAY first")]
    [DataRow(new byte[] { 0x00, 0x04, 0x02, 0x06, 0x40 }, H3FrameError, DisplayName = "SETTINGS ending part way through a value")]
    [DataRow(new byte[] { 0x00, 0x04, 0x01, 0x40 }, H3FrameError, DisplayName = "SETTINGS ending part way through an identifier")]
    [DataRow(new byte[] { 0x00, 0x04, 0x02, 0x02, 0x00 }, H3SettingsError, DisplayName = "an HTTP/2 setting")]
    [DataRow(new byte[] { 0x00, 0x04, 0x04, 0x06, 0x01, 0x06, 0x02 }, H3SettingsError, DisplayName = "a repeated setting")]
    public async Task ServeAsync_ControlStreamBreaksASettingsRule_ClosesWithItsCode(byte[] controlStream, ulong code)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, settings: controlStream[1..]);

        await client.FinishAsync(Token);

        Assert.AreEqual(code, client.CloseCode);
    }

    [TestMethod]
    [DataRow(Http3FrameType.Data, H3FrameUnexpected)]
    [DataRow(Http3FrameType.Headers, H3FrameUnexpected)]
    [DataRow(0x08UL, H3FrameUnexpected, DisplayName = "HTTP/2's WINDOW_UPDATE")]
    public async Task ServeAsync_FrameNotAllowedOnTheControlStream_ClosesWithFrameUnexpected(ulong type, ulong code)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(Http3TestClient.ControlStreamId, Http3TestClient.Frame(type, [0x00]));
        await client.FinishAsync(Token);

        Assert.AreEqual(code, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_GoAwayMaxPushIdAndUnknownFramesOnTheControlStream_AreReadAndIgnored()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, settings: [0x04, 0x04, 0x01, 0x00, 0x21, 0x07]);

        client.Send(Http3TestClient.ControlStreamId, [
            .. Http3TestClient.Frame(Http3FrameType.GoAway, [0x00]),
            .. Http3TestClient.Frame(Http3FrameType.MaxPushId, [0x00]),
            .. Http3TestClient.Frame(0x21, [0x01, 0x02])]);
        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual(FileBody, client.Response(0).BodyText);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "The client sent GOAWAY.");
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ControlFrameLongerThanMaxMessage_ClosesWithExcessiveLoad()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { MaxMessageBytes = 200 });

        client.Send(Http3TestClient.ControlStreamId, Http3TestClient.Frame(0x21, new byte[201]));
        await client.FinishAsync(Token);

        Assert.AreEqual(H3ExcessiveLoad, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ControlStreamResetPartWayThroughAFrame_ClosesWithClosedCriticalStream()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(Http3TestClient.ControlStreamId, Http3TestClient.Frame(Http3FrameType.GoAway, [0x00]));
        await client.UntilAsync(() => client.Log.Notes.Contains("The client sent GOAWAY."), Token);
        client.Send(Http3TestClient.ControlStreamId, [0x07, 0x05]);
        client.Reset(Http3TestClient.ControlStreamId);
        await client.FinishAsync(Token);

        Assert.AreEqual(H3ClosedCriticalStream, client.CloseCode);
    }

    [TestMethod]
    [DataRow(Http3TestClient.ControlStreamId)]
    [DataRow(Http3TestClient.EncoderStreamId)]
    [DataRow(Http3TestClient.DecoderStreamId)]
    public async Task ServeAsync_ClientClosesACriticalStream_ClosesWithClosedCriticalStream(ulong streamId)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(streamId, [], fin: true);
        await client.FinishAsync(Token);

        Assert.AreEqual(H3ClosedCriticalStream, client.CloseCode);
    }

    [TestMethod]
    [DataRow((byte)0x00, DisplayName = "a second control stream")]
    [DataRow((byte)0x02, DisplayName = "a second QPACK encoder stream")]
    [DataRow((byte)0x01, DisplayName = "a push stream")]
    public async Task ServeAsync_ClientOpensAStreamItMayNot_ClosesWithStreamCreationError(byte type)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, openQpackStreams: false);

        client.Send(Http3TestClient.EncoderStreamId, [0x02]);
        client.Send(Http3TestClient.DecoderStreamId, [type]);
        await client.FinishAsync(Token);

        Assert.AreEqual(H3StreamCreationError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_StreamOfUnknownType_IsStoppedWithStreamCreationErrorAndTheConnectionCarriesOn()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, openQpackStreams: false);

        client.Send(Http3TestClient.EncoderStreamId, [0x21, 0x01]);
        client.Send(Http3TestClient.DecoderStreamId, [], fin: true);
        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0) && client.Log.Notes.Contains("Stream 10 ended before its stream type.") && client.StopSendingCode(Http3TestClient.EncoderStreamId) is not null, Token);

        Assert.AreEqual(H3StreamCreationError, client.StopSendingCode(Http3TestClient.EncoderStreamId));
        Assert.AreEqual(FileBody, client.Response(0).BodyText);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    [DataRow(Http3TestClient.EncoderStreamId, new byte[] { 0x3f, 0x45 }, QpackEncoderStreamError, DisplayName = "a dynamic table capacity past the 0 advertised")]
    [DataRow(Http3TestClient.DecoderStreamId, new byte[] { 0x80 }, QpackDecoderStreamError, DisplayName = "a Section Acknowledgment the encoder never asked for")]
    public async Task ServeAsync_QpackStreamBreaksARule_ClosesWithItsQpackCode(ulong streamId, byte[] instructions, ulong code)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(streamId, instructions);
        await client.FinishAsync(Token);

        Assert.AreEqual(code, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_QpackStreamsCarryAcceptedInstructions_TheConnectionCarriesOn()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(Http3TestClient.EncoderStreamId, [0x20]);
        client.Send(Http3TestClient.DecoderStreamId, [0x40]);
        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual(FileBody, client.Response(0).BodyText);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ClientResetsAStreamBeforeItsType_NotesItAndTheConnectionCarriesOn()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, openQpackStreams: false);

        client.Reset(Http3TestClient.EncoderStreamId);
        await client.UntilAsync(() => client.Log.Notes.Any(note => note.StartsWith("Stream 6 was reset by the client", StringComparison.Ordinal)), Token);
        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual(FileBody, client.Response(0).BodyText);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ClientAllowsNoUnidirectionalStreams_ClosesWithGeneralProtocolError()
    {
        using var client = new Http3TestClient(UnitTestClientHelloEditsForHttp3.NoUnidirectionalStreams);
        _ = client.Serve(Token);

        await client.CompleteHandshakeAsync(Token);
        await client.FinishAsync(Token);

        Assert.AreEqual(H3GeneralProtocolError, client.CloseCode);
        Assert.IsTrue(client.Log.Notes.Any(note => note.Contains("initial_max_streams_uni", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesTheConnection_TheServerStopsWithoutAClose()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);
        client.Send(0, client.Request("GET", "/file.txt"));

        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicConnectionCloseFrame(true, 0x100, 0, ReadOnlyMemory<byte>.Empty)));
        await client.FinishAsync(Token);

        Assert.IsNull(client.CloseCode);
        Assert.IsFalse(client.HasEnded(0));
    }
}

/// <summary>A same-length edit of curl's recorded ClientHello for the HTTP/3 tests.</summary>
internal static class UnitTestClientHelloEditsForHttp3
{
    /// <summary>initial_max_streams_uni (0x09) 262144, as every recorded build sends it, becomes 0, still four bytes long.</summary>
    public static byte[] NoUnidirectionalStreams(byte[] hello)
    {
        var from = Convert.FromHexString("090480040000");
        var index = hello.AsSpan().IndexOf(from);
        Assert.IsGreaterThanOrEqualTo(0, index);
        var copy = hello.ToArray();
        Convert.FromHexString("090480000000").CopyTo(copy, index);
        return copy;
    }
}
