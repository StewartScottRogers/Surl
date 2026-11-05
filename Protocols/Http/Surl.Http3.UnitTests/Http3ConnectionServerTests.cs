using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Http3;

/// <summary>
/// Drives <see cref="Http3ConnectionServer"/> with a handler of the test's own, over the in-memory
/// QUIC test connection (<see cref="Http3TestQuicClient"/>): pinned upstream curl's recorded
/// Initials, then HTTP/3 frames written from RFC 9114 and RFC 9204. No HTTP server is involved.
/// </summary>
[TestClass]
public sealed class Http3ConnectionServerTests
{
    private const ulong H3NoError = 0x100;
    private const ulong H3InternalError = 0x102;
    private const ulong H3MessageError = 0x10e;

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task ServeAsync_RequestStream_IsHandedToTheHandlerWhichAnswersItWithNoHttpServer()
    {
        using var client = new Http3TestQuicClient();
        Http3RequestHead? head = null;
        var handler = new UnitTestRequestStreamHandler(async (stream, token) =>
        {
            head = stream.Head;
            var body = await ReadBodyAsync(stream, token);
            await stream.WriteHeadersAsync([new(":status", "200"), new("content-type", "text/plain")], endStream: false, token);
            await stream.WriteDataAsync(Encoding.ASCII.GetBytes("echo " + body), endStream: true, token);
        });
        await StartAsync(client, handler);

        client.Send(0, [.. client.Request("POST", "/echo", ("content-length", "5")), .. Http3TestQuicClient.Frame(Http3FrameType.Data, Encoding.ASCII.GetBytes("hello"))], fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("POST", head!.Method);
        Assert.AreEqual("/echo", head.Target);
        Assert.AreEqual("127.0.0.1:18050", head.Authority);
        Assert.AreEqual(5L, head.ContentLength);
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(new[] { "content-type: text/plain" }, response.RegularFields);
        Assert.AreEqual("echo hello", response.BodyText);
        Assert.IsTrue(response.Fin);
        Assert.IsNull(client.StopSendingCode(0), "The request was read to its end.");
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_HandlerReturnsWithTheBodyUnread_StopsTheRequestWithNoError()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestRequestStreamHandler((stream, token) =>
            stream.WriteHeadersAsync([new(":status", "204")], endStream: true, token));
        await StartAsync(client, handler);

        client.Send(0, client.Request("PUT", "/", ("content-length", "100")));
        await client.UntilAsync(() => client.StopSendingCode(0) is not null, Token);

        Assert.AreEqual("204", client.Response(0).Status);
        Assert.AreEqual(H3NoError, client.StopSendingCode(0));
        Assert.IsNull(client.ResetCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_HandlerResetsTheStream_ResetsAndStopsItOnceWithTheCode()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestRequestStreamHandler((stream, token) =>
            stream.ResetAsync(Http3ErrorCode.MessageError, "the test refused it", token));
        var log = new RecordingExchangeLog();
        await StartAsync(client, handler, log: log);

        client.Send(0, client.Request("PUT", "/", ("content-length", "100")));
        await client.UntilAsync(() => client.StopSendingCode(0) is not null, Token);

        Assert.AreEqual(H3MessageError, client.ResetCode(0));
        Assert.AreEqual(H3MessageError, client.StopSendingCode(0));
        Assert.IsTrue(log.Notes.Contains("Stream 0 was reset with MessageError: the test refused it."));
    }

    [TestMethod]
    public async Task ServeAsync_HandlerResetsTheResponseAlone_ConnectionStopsTheStillArrivingRequestWithNoError()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestRequestStreamHandler(async (stream, token) =>
        {
            await stream.WriteHeadersAsync([new(":status", "200")], endStream: false, token);
            await stream.ResetResponseAsync(Http3ErrorCode.InternalError, token);
        });
        await StartAsync(client, handler);

        client.Send(0, client.Request("PUT", "/", ("content-length", "100")));
        await client.UntilAsync(() => client.StopSendingCode(0) is not null, Token);

        Assert.AreEqual(H3InternalError, client.ResetCode(0));
        Assert.AreEqual(H3NoError, client.StopSendingCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_BodyWrittenThroughTheResponseBodyStream_GoesOutAsDataFramesThenFin()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestRequestStreamHandler(async (stream, token) =>
        {
            await stream.WriteHeadersAsync([new(":status", "200")], endStream: false, token);
            await stream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: false, token);
            await using (var body = stream.CreateResponseBodyStream())
            {
                await body.WriteAsync(Encoding.ASCII.GetBytes("one "), token);
                await body.WriteAsync(Encoding.ASCII.GetBytes("two"), token);
            }

            await stream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true, token);
        });
        await StartAsync(client, handler);

        client.Send(0, client.Request("GET", "/"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var frames = Http3TestQuicClient.ReadFrames(client.StreamSent(0).Data);
        CollectionAssert.AreEqual(new[] { Http3FrameType.Headers, Http3FrameType.Data, Http3FrameType.Data }, frames.Select(frame => frame.Type).ToArray());
        Assert.AreEqual("one two", client.Response(0).BodyText);
        Assert.IsTrue(client.Response(0).Fin);
    }

    [TestMethod]
    public async Task ServeAsync_RequestStreamResetMidBody_EndsTheBodyEarlyWithNoContentLengthMismatch()
    {
        using var client = new Http3TestQuicClient();
        var firstPiece = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource<(bool EndedEarly, bool Mismatch, byte[]? After)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestRequestStreamHandler(async (stream, token) =>
        {
            await stream.ReadBodyAsync(token);
            firstPiece.SetResult();
            await ReadBodyAsync(stream, token);
            ended.SetResult((stream.HasRequestBodyEndedEarly, stream.HasContentLengthMismatch, await stream.ReadBodyAsync(token)));
        });
        await StartAsync(client, handler);

        client.Send(0, [.. client.Request("PUT", "/", ("content-length", "10")), .. Http3TestQuicClient.Frame(Http3FrameType.Data, [1, 2, 3])]);
        await firstPiece.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        client.Reset(0);
        var (endedEarly, mismatch, after) = await ended.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.IsTrue(endedEarly);
        Assert.IsFalse(mismatch);
        Assert.IsNull(after);
    }

    [TestMethod]
    public async Task ServeAsync_HeadersFrameLongerThanMaxMessageWithNoServerField_Answers431WithContentLengthAlone()
    {
        using var client = new Http3TestQuicClient();
        var handler = new UnitTestRequestStreamHandler((_, _) => throw new AssertFailedException("The request must not reach the handler."));
        await StartAsync(client, handler, ExchangeLimits.Default with { MaxMessageBytes = 200 });

        client.Send(0, client.Request("GET", "/", ("x-long", new string('a', 300))), fin: true);
        await client.UntilAsync(() => client.StopSendingCode(0) is not null, Token);

        var refused = client.Response(0);
        Assert.AreEqual("431", refused.Status);
        CollectionAssert.AreEqual(new[] { "content-length: 0" }, refused.RegularFields);
        Assert.AreEqual(H3NoError, client.StopSendingCode(0));
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var handler = new UnitTestRequestStreamHandler((_, _) => Task.CompletedTask);
        var context = Context(new RecordingExchangeLog(), TimeProvider.System, ExchangeLimits.Default);

        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ConnectionServer(null!, new Http3ServerOptions(), context));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ConnectionServer(handler, null!, context));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ConnectionServer(handler, new Http3ServerOptions(), null!));
    }

    [TestMethod]
    public async Task WriteHeadersAsync_NullFieldLines_Throws()
    {
        using var client = new Http3TestQuicClient();
        var thrown = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestRequestStreamHandler(async (stream, token) =>
        {
            thrown.SetResult(await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => stream.WriteHeadersAsync(null!, endStream: true, token)));
            await stream.WriteHeadersAsync([new(":status", "204")], endStream: true, token);
        });
        await StartAsync(client, handler);

        client.Send(0, client.Request("GET", "/"), fin: true);

        Assert.IsNotNull(await thrown.Task.WaitAsync(TimeSpan.FromSeconds(30), Token));
    }

    private static async Task<string> ReadBodyAsync(Http3RequestStream stream, CancellationToken cancellationToken)
    {
        var body = new List<byte>();
        while (await stream.ReadBodyAsync(cancellationToken) is { } piece)
        {
            body.AddRange(piece);
        }

        return Encoding.ASCII.GetString([.. body]);
    }

    private async Task StartAsync(Http3TestQuicClient client, IHttp3RequestStreamHandler handler, ExchangeLimits? limits = null, RecordingExchangeLog? log = null)
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
        var context = Context(log ?? new RecordingExchangeLog(), time, limits ?? ExchangeLimits.Default);
        _ = client.Serve(new Http3ConnectionServer(handler, new Http3ServerOptions(), context), time, Token);
        await client.OpenAsync(Token);
    }

    private static ExchangeContext Context(IExchangeLog log, TimeProvider time, ExchangeLimits limits) => new(
        1,
        new ListenUrl("https", "127.0.0.1", 18050).WithBoundPort(18050),
        new IPEndPoint(IPAddress.Loopback, 18050),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        time,
        CancellationToken.None)
    {
        Limits = limits,
    };
}

/// <summary>A request stream handler that runs the test's script.</summary>
internal sealed class UnitTestRequestStreamHandler(Func<Http3RequestStream, CancellationToken, Task> script) : IHttp3RequestStreamHandler
{
    public Task ServeRequestStreamAsync(Http3RequestStream stream, CancellationToken cancellationToken) => script(stream, cancellationToken);
}
