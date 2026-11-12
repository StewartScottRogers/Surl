using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>Request streams (RFC 9114 section 4): their frames, bodies, limits and endings.</summary>
public sealed partial class Http3ConnectionServerTests
{
    private const ulong H3InternalError = 0x102;
    private const ulong H3RequestIncomplete = 0x10d;

    [TestMethod]
    public async Task ServeAsync_UnknownFrameBeforeHeaders_IsSkipped()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, [.. Http3TestClient.Frame(0x21, [0x01, 0x02, 0x03]), .. client.Request("GET", "/file.txt")], fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual(FileBody, client.Response(0).BodyText);
    }

    [TestMethod]
    [DataRow(Http3FrameType.Data, DisplayName = "DATA before HEADERS")]
    [DataRow(Http3FrameType.Settings, DisplayName = "SETTINGS on a request stream")]
    [DataRow(0x06UL, DisplayName = "HTTP/2's PING")]
    public async Task ServeAsync_FrameNotAllowedBeforeHeaders_ClosesWithFrameUnexpected(ulong type)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, Http3TestClient.Frame(type, [0x00]), fin: true);
        await client.FinishAsync(Token);

        Assert.AreEqual(H3FrameUnexpected, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_UnknownFrameBeforeHeadersLongerThanMaxMessage_ClosesWithExcessiveLoad()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { MaxMessageBytes = 200 });

        client.Send(0, Http3TestClient.Frame(0x21, new byte[201]));
        await client.FinishAsync(Token);

        Assert.AreEqual(H3ExcessiveLoad, client.CloseCode);
    }

    [TestMethod]
    [DataRow("no frame", DisplayName = "FIN with no frame")]
    [DataRow("unknown frame only", DisplayName = "FIN after an unknown frame")]
    public async Task ServeAsync_StreamEndsBeforeHeaders_ResetsAndStopsWithRequestIncomplete(string sent)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, sent == "no frame" ? [] : Http3TestClient.Frame(0x21, []), fin: true);
        await client.UntilAsync(() => client.ResetCode(0) is not null && client.StopSendingCode(0) is not null, Token);

        Assert.AreEqual(H3RequestIncomplete, client.ResetCode(0));
        Assert.AreEqual(H3RequestIncomplete, client.StopSendingCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_HeadersNotWholeWithinTheHeadTimeout_ResetsAndStopsWithRequestIncomplete()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { HeadTimeout = TimeSpan.FromSeconds(5) });
        var headers = client.Request("GET", "/file.txt");

        client.Send(0, headers[..3]);
        await client.UntilAsync(
            () =>
            {
                client.Time.Advance(TimeSpan.FromMilliseconds(100));
                return client.ResetCode(0) is not null && client.StopSendingCode(0) is not null;
            },
            Token);

        Assert.AreEqual(H3RequestIncomplete, client.ResetCode(0));
        Assert.IsTrue(client.Log.Notes.Contains("Stream 0 was reset with RequestIncomplete: its HEADERS was not whole within the head timeout."));
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_StreamEndsPartWayThroughHeaders_ClosesWithFrameError()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt")[..3], fin: true);
        await client.FinishAsync(Token);

        Assert.AreEqual(H3FrameError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ClientResetsPartWayThroughHeaders_StopsTheStreamQuietly()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt")[..3]);
        client.Reset(0);
        client.Send(4, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(4) && client.Log.Notes.Any(note => note.StartsWith("Stream 0 was reset by the client", StringComparison.Ordinal)), Token);

        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_FieldSectionQpackCannotDecode_ClosesWithQpackDecompressionFailed()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, Http3TestClient.Frame(Http3FrameType.Headers, [0x00, 0x00, 0xff]), fin: true);
        await client.FinishAsync(Token);

        Assert.AreEqual(QpackDecompressionFailed, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_FieldSectionDecodingPastMaxMessage_Answers431ThenStops()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { MaxMessageBytes = 100 });

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0) && client.StopSendingCode(0) is not null, Token);

        Assert.AreEqual("431", client.Response(0).Status);
        Assert.AreEqual(H3NoError, client.StopSendingCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_NoMaxMessage_SendsEmptySettingsAndAnswers()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { MaxMessageBytes = 0 });

        client.Send(0, [.. Http3TestClient.Frame(0x21, [0x01]), .. client.Request("GET", "/file.txt")], fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x04, 0x00 }, client.StreamSent(Http3TestClient.ServerControlStreamId).Data);
        Assert.AreEqual(FileBody, client.Response(0).BodyText);
    }

    [TestMethod]
    public async Task ServeAsync_GetWithABodyInPiecesUnknownFramesAndTrailers_ReadsItAllAndAnswers()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, [.. client.Request("GET", "/file.txt", ("content-length", "8")), .. Http3TestClient.Frame(Http3FrameType.Data, Ascii("abcd"))]);
        client.Send(0, [.. Http3TestClient.Frame(0x21, [0x09]), .. Http3TestClient.Frame(Http3FrameType.Data, Ascii("efgh"))]);
        client.Send(0, client.Headers(("x-trailer", "1")), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual(FileBody, client.Response(0).BodyText);
        Assert.IsNull(client.StopSendingCode(0), "The body was read to its end.");
    }

    [TestMethod]
    public async Task ServeAsync_GetWithAnUnannouncedBody_ReadsItAndAnswers()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, [.. client.Request("GET", "/file.txt"), .. Http3TestClient.Frame(Http3FrameType.Data, Ascii("abcd"))], fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual(FileBody, client.Response(0).BodyText);
    }

    [TestMethod]
    public async Task ServeAsync_DropRuleAtTheCommandStage_ResetsTheRequestStreamInternalErrorUnanswered()
    {
        using var client = new Http3TestClient { Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command }) };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(0x102UL, client.ResetCode(0));
    }
    [TestMethod]
    public async Task ServeAsync_DataNotAddingUpToContentLength_ResetsAndStopsWithMessageError()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, [.. client.Request("GET", "/file.txt", ("content-length", "8")), .. Http3TestClient.Frame(Http3FrameType.Data, Ascii("abcd"))], fin: true);
        await client.UntilAsync(() => client.ResetCode(0) is not null && client.StopSendingCode(0) is not null, Token);

        Assert.AreEqual(H3MessageError, client.ResetCode(0));
    }

    [TestMethod]
    [DataRow(Http3FrameType.GoAway, H3FrameUnexpected, DisplayName = "GOAWAY in the body")]
    [DataRow(Http3FrameType.Headers, QpackDecompressionFailed, DisplayName = "trailers QPACK cannot decode")]
    public async Task ServeAsync_BodyBreaksARule_ClosesWithItsCode(ulong type, ulong code)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, [.. client.Request("GET", "/file.txt", ("content-length", "1")), .. Http3TestClient.Frame(type, [0x00, 0x00, 0xff])], fin: true);
        await client.FinishAsync(Token);

        Assert.AreEqual(code, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_FrameAfterTrailers_ClosesWithFrameUnexpected()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, [.. client.Request("GET", "/file.txt", ("content-length", "0"), ("expect", "100-continue")), .. client.Headers(("x-trailer", "1")), .. Http3TestClient.Frame(0x21, [])], fin: true);
        await client.FinishAsync(Token);

        Assert.AreEqual(H3FrameUnexpected, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_BodyFrameLongerThanMaxMessage_ClosesWithExcessiveLoad()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { MaxMessageBytes = 300 });

        client.Send(0, [.. client.Request("GET", "/file.txt", ("content-length", "1")), .. Http3TestClient.Frame(0x21, new byte[301])]);
        await client.FinishAsync(Token);

        Assert.AreEqual(H3ExcessiveLoad, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ClientResetsPartWayThroughTheBody_TheRequestIsRefusedAsIncomplete()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt", ("content-length", "8"), ("expect", "100-continue")));
        await client.UntilAsync(() => client.StreamSent(0).Data.Length > 0, Token);
        client.Send(0, [0x00, 0x08, .. Ascii("abc")]);
        client.Reset(0);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual("400", client.Response(0).Status);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ExpectContinue_SendsAnInterim100BeforeReadingTheBody()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt", ("content-length", "2"), ("expect", "100-continue")));
        await client.UntilAsync(() => client.StreamSent(0).Data.Length > 0, Token);
        client.Send(0, Http3TestClient.Frame(Http3FrameType.Data, Ascii("hi")), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("100", response.FieldSections[0].Single(line => line.Name == ":status").Value);
        Assert.AreEqual("200", response.Status);
        Assert.AreEqual(FileBody, response.BodyText);
    }

    [TestMethod]
    [DataRow("content-length", DisplayName = "a content-length past the limit")]
    [DataRow("data", DisplayName = "DATA past the limit")]
    public async Task ServeAsync_UploadPastMaxFilesize_Answers413ThenStops(string past)
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { MaxUploadBytes = 4 }, allowUploads: true);
        (string, string)[] extra = past == "content-length" ? [("content-length", "5")] : [];

        client.Send(0, [.. client.Request("GET", "/file.txt", extra), .. Http3TestClient.Frame(Http3FrameType.Data, Ascii("hello"))]);
        await client.UntilAsync(() => client.HasEnded(0) && client.StopSendingCode(0) is not null, Token);

        Assert.AreEqual("413", client.Response(0).Status);
        Assert.AreEqual(H3NoError, client.StopSendingCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_BytesAnswer_IsSentAsOneDataFrame()
    {
        using var client = new Http3TestClient();
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "empty.txt"), [], FileTime);
        await client.StartAsync(Token, fileSystem: fileSystem);

        client.Send(0, client.Request("HEAD", "/file.txt"), fin: true);
        client.Send(4, client.Request("GET", "/empty.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0) && client.HasEnded(4), Token);

        Assert.AreEqual("200", client.Response(0).Status);
        Assert.IsEmpty(client.Response(0).Body);
        Assert.AreEqual("200", client.Response(4).Status);
        Assert.IsEmpty(client.Response(4).Body);
    }

    [TestMethod]
    [DataRow("shrinks")]
    [DataRow("cannot be read")]
    [DataRow("access denied")]
    public async Task ServeAsync_FileThatCannotBeSentWhole_ResetsTheStreamInternalError(string failure)
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

        using var client = new Http3TestClient();
        await client.StartAsync(Token, fileSystem: fileSystem);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(H3InternalError, client.ResetCode(0));
        Assert.IsTrue(client.Log.Notes.Any(note => note.EndsWith("stream 0 was reset with H3_INTERNAL_ERROR.", StringComparison.Ordinal)));
        Assert.IsNull(client.CloseCode);
    }
}
