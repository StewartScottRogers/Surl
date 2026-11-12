using Surl.Protocol.Abstractions;
using Surl.Quic;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>Answers a handler other than <see cref="HttpRequestAnswerer"/> gives: bytes, and an answer the client no longer wants.</summary>
public sealed partial class Http3ConnectionServerTests
{
    [TestMethod]
    public async Task ServeAsync_HandlerAnswersWithBytes_SendsHeadersThenOneDataFrameWithFin()
    {
        using var client = new Http3TestClient
        {
            Handler = new UnitTestScriptedHandler((_, _) => Task.FromResult(new HttpAnswer(HttpStatus.Ok, [new("Content-Length", "2")], new HttpAnswerBody.Bytes(Ascii("hi")), false, false))),
        };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(new[] { "content-length: 2" }, response.RegularFields);
        Assert.AreEqual("hi", response.BodyText);
        Assert.IsTrue(response.Fin);
    }

    [TestMethod]
    public async Task ServeAsync_GetWithSeveralRanges_AnswersTheMultipartByteRangesHttp11Answers()
    {
        using var client = new Http3TestClient();
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt", ("range", "bytes=0-1,3-4")), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        var (http11Fields, http11Body) = Http11Answer(RecordedResponse("range-multiple-206"));
        Assert.AreEqual("206", response.Status);
        CollectionAssert.AreEqual(http11Fields, response.RegularFields);
        Assert.AreEqual(http11Body, response.BodyText);
        Assert.AreEqual(Latin1(RecordedFixture.ReadBytes("range-multiple-206", "stdout.bin")), response.BodyText);
        Assert.IsTrue(response.Fin);
    }

    [TestMethod]
    public async Task ServeAsync_ClientStopsTheStreamBeforeItsAnswer_NotesItAndTheConnectionCarriesOn()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new Http3TestClient
        {
            Handler = new UnitTestScriptedHandler(async (_, token) =>
            {
                await release.Task.WaitAsync(token);
                return new HttpAnswer(HttpStatus.Ok, [], new HttpAnswerBody.Bytes(Ascii("hi")), false, false);
            }),
        };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/"), fin: true);
        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicStopSendingFrame(0, 0x10c)));
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);
        release.SetResult();
        await client.UntilAsync(() => client.Log.Notes.Any(note => note.StartsWith("The client stopped stream 0 with STOP_SENDING 0x10c", StringComparison.Ordinal)), Token);

        Assert.AreEqual(0x10cUL, client.ResetCode(0), "QUIC answers STOP_SENDING with RESET_STREAM carrying the client's code.");
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_HandlerAnswersWithEmptyBytes_EndsTheStreamOnItsHeaders()
    {
        using var client = new Http3TestClient
        {
            Handler = new UnitTestScriptedHandler((_, _) => Task.FromResult(new HttpAnswer(HttpStatus.Ok, [], new HttpAnswerBody.Bytes(ReadOnlyMemory<byte>.Empty), false, false))),
        };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("200", response.Status);
        Assert.IsEmpty(response.Body);
        Assert.HasCount(1, Http3TestClient.ReadFrames(client.StreamSent(0).Data));
    }

    [TestMethod]
    public async Task ServeAsync_HandlerCopiesOrHashesTheBodyThenReadsItAgain_GetsItWholeThenNothingMore()
    {
        var copy = new MemoryStream();
        var outcomes = new List<HttpRequestBodyDiscardOutcome>();
        string? hashed = null;
        using var client = new Http3TestClient
        {
            Handler = new UnitTestScriptedHandler(async (request, token) =>
            {
                if (request.Target == "/hash")
                {
                    using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
                    await request.Body.DiscardAsync(hash, token);
                    hashed = Convert.ToHexString(hash.GetHashAndReset());
                    return new HttpAnswer(HttpStatus.Ok, [], HttpAnswerBody.None, false, false);
                }

                outcomes.Add(await request.Body.CopyToAsync(copy, token));
                outcomes.Add(await request.Body.DiscardAsync(null, token));
                return new HttpAnswer(HttpStatus.Ok, [], HttpAnswerBody.None, false, false);
            }),
        };
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { MaxUploadBytes = 0 });

        client.Send(0, [.. client.Request("PUT", "/", ("expect", "100-continue")), .. Http3TestClient.Frame(Http3FrameType.Data, Ascii("payload"))], fin: true);
        client.Send(4, [.. client.Request("PUT", "/hash"), .. Http3TestClient.Frame(Http3FrameType.Data, Ascii("payload"))], fin: true);
        await client.UntilAsync(() => client.HasEnded(0) && client.HasEnded(4), Token);

        Assert.AreEqual("payload", Latin1(copy.ToArray()));
        Assert.AreEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Ascii("payload"))), hashed);
        CollectionAssert.AreEqual(new[] { HttpRequestBodyDiscardOutcome.Discarded, HttpRequestBodyDiscardOutcome.Discarded }, outcomes);
        Assert.AreEqual("200", client.Response(0).Status);
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Http3HttpRequestBody(null!, 0).CopyToAsync(null!, Token));
    }
}

/// <summary>A request handler that runs the test's script.</summary>
internal sealed class UnitTestScriptedHandler(Func<HttpRequest, CancellationToken, Task<HttpAnswer>> script) : IHttpRequestHandler
{
    public Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken) => script(request, cancellationToken);
}
