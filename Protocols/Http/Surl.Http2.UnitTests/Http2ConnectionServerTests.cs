using System.Net;
using Surl.Hpack;
using Surl.Protocol.Abstractions;

namespace Surl.Http2;

/// <summary>
/// The connection with a handler of the test's own, as an extended server such as gRPC uses it
/// (ADR-0086, decision 1). The HTTP server's use of it, with bytes recorded from pinned upstream
/// curl, is tested in <c>Surl.Protocol.Http.UnitTests</c>.
/// </summary>
[TestClass]
public sealed class Http2ConnectionServerTests
{
    public TestContext TestContext { get; set; } = null!;

    // RFC 9113 section 8.1: the response is a HEADERS, DATA, and a trailing HEADERS whose
    // END_STREAM ends the stream; the client's request ended on its own HEADERS.
    [TestMethod]
    public async Task ServeAsync_HandlerAnsweringWithHeadersDataAndTrailers_WritesThemInOrderEndingOnTheTrailers()
    {
        var client = new Http2TestClient();
        var connection = new InMemoryConnection([Http2TestClient.Concat(Http2TestClient.Settings(), client.Get(1, path: "/echo.Echo/Say"))]);
        var handler = new TrailersHandler();

        await new Http2ConnectionServer(handler, Context(new RecordingExchangeLog()), connection).ServeAsync();

        var frames = Http2TestClient.Frames(connection.WrittenBytes).Where(frame => frame.StreamId == 1).ToArray();
        Assert.HasCount(3, frames);
        Assert.IsTrue(frames[0] is Http2HeadersFrame { EndStream: false, EndHeaders: true });
        Assert.IsTrue(frames[1] is Http2DataFrame { EndStream: false });
        Assert.IsTrue(frames[2] is Http2HeadersFrame { EndStream: true, EndHeaders: true });
        var response = Http2TestClient.Responses(connection.WrittenBytes)[1];
        Assert.HasCount(2, response.HeaderBlocks);
        Assert.AreEqual("200", response.HeaderBlocks[0].Single(field => field.Name == ":status").Value);
        Assert.AreEqual("payload", response.BodyText);
        Assert.AreEqual("grpc-status: 0", string.Join(", ", response.Fields.Select(field => $"{field.Name}: {field.Value}")));
        Assert.IsTrue(response.EndedOnHeaders);
        Assert.AreEqual("/echo.Echo/Say", handler.Path);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_HandlerReturningWhileTheBodyIsStillArriving_IsFollowedByRstStreamNoError()
    {
        var client = new Http2TestClient();
        var connection = new InMemoryConnection([Http2TestClient.Concat(Http2TestClient.Settings(), client.Get(1, endStream: false))]);

        await new Http2ConnectionServer(new TrailersHandler(), Context(new RecordingExchangeLog()), connection).ServeAsync();

        var response = Http2TestClient.Responses(connection.WrittenBytes)[1];
        Assert.IsTrue(response.EndedOnHeaders);
        Assert.AreEqual(Http2ErrorCode.NoError, response.ResetCode);
    }

    [TestMethod]
    public async Task ServeUpgradedAsync_UpgradedRequest_IsHandedToTheHandlerAsStreamOne()
    {
        var connection = new InMemoryConnection([Http2TestClient.Start()]);
        var handler = new TrailersHandler();
        var head = new Http2RequestHead("GET", "/upgraded", "127.0.0.1:18050", [], ContentLength: null, ExpectsContinue: false);

        await new Http2ConnectionServer(handler, Context(new RecordingExchangeLog()), connection).ServeUpgradedAsync(head, []);

        Assert.AreEqual("/upgraded", handler.Path);
        Assert.IsTrue(Http2TestClient.Responses(connection.WrittenBytes)[1].EndedOnHeaders);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var context = Context(new RecordingExchangeLog());
        var connection = new InMemoryConnection([]);

        Assert.ThrowsExactly<ArgumentNullException>(() => new Http2ConnectionServer(null!, context, connection));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http2ConnectionServer(new TrailersHandler(), null!, connection));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http2ConnectionServer(new TrailersHandler(), context, null!));
    }

    // ADR-0116 amendment 1: a handler caught by the exchange's cancellation ends its stream with a
    // farewell write of its own deadline, which comes before the connection's GOAWAY NO_ERROR.
    [TestMethod]
    public async Task ServeAsync_ShutdownWhileAHandlerWaits_LetsItWriteFarewellTrailersBeforeTheGoAway()
    {
        var client = new Http2TestClient();
        var connection = new InMemoryConnection([Http2TestClient.Concat(Http2TestClient.Settings(), client.Get(1, endStream: false))], peerHalfClosesWhenExhausted: false);
        var handler = new FarewellHandler(farewellReset: false);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var context = Context(new RecordingExchangeLog()) with { CancellationToken = shutdown.Token, ShutdownToken = shutdown.Token };
        var serving = new Http2ConnectionServer(handler, context, connection).ServeAsync();

        await handler.Waiting.Task.WaitAsync(TestContext.CancellationToken);
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        var frames = Http2TestClient.Frames(connection.WrittenBytes);
        Assert.IsTrue(frames[^2] is Http2HeadersFrame { StreamId: 1, EndStream: true });
        Assert.IsTrue(frames[^1] is Http2GoAwayFrame { LastStreamId: 1, ErrorCode: Http2ErrorCode.NoError });
        Assert.AreEqual(":status: 200, grpc-status: 14", string.Join(", ", Http2TestClient.Responses(connection.WrittenBytes)[1].Fields.Select(field => $"{field.Name}: {field.Value}")));
        Assert.IsTrue(handler.SawExchangeCancelled);
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownWhileAHandlerWaits_LetsItWriteAFarewellResetBeforeTheGoAway()
    {
        var client = new Http2TestClient();
        var connection = new InMemoryConnection([Http2TestClient.Concat(Http2TestClient.Settings(), client.Get(1, endStream: false))], peerHalfClosesWhenExhausted: false);
        var handler = new FarewellHandler(farewellReset: true);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var context = Context(new RecordingExchangeLog()) with { CancellationToken = shutdown.Token, ShutdownToken = shutdown.Token };
        var serving = new Http2ConnectionServer(handler, context, connection).ServeAsync();

        await handler.Waiting.Task.WaitAsync(TestContext.CancellationToken);
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        var frames = Http2TestClient.Frames(connection.WrittenBytes);
        Assert.IsTrue(frames[^2] is Http2RstStreamFrame { StreamId: 1, ErrorCode: Http2ErrorCode.Cancel });
        Assert.IsTrue(frames[^1] is Http2GoAwayFrame { LastStreamId: 1, ErrorCode: Http2ErrorCode.NoError });
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesWhileAHandlerWaits_IsNotAnExchangeCancellation()
    {
        var client = new Http2TestClient();
        var connection = new InMemoryConnection([Http2TestClient.Concat(Http2TestClient.Settings(), client.Get(1, endStream: false))]);
        var handler = new FarewellHandler(farewellReset: false);

        await new Http2ConnectionServer(handler, Context(new RecordingExchangeLog()), connection).ServeAsync();

        Assert.IsFalse(handler.SawExchangeCancelled);
    }

    private ExchangeContext Context(IExchangeLog log) => new(
        1,
        new ListenUrl("http", "127.0.0.1", 18050).WithBoundPort(18050),
        new IPEndPoint(IPAddress.Loopback, 18050),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        TimeProvider.System,
        TestContext.CancellationToken);

    // Waits for the request body to end; when the exchange's cancellation cuts the wait off, ends
    // the stream with farewell trailers grpc-status 14 or a farewell RST_STREAM CANCEL.
    private sealed class FarewellHandler(bool farewellReset) : IHttp2StreamHandler
    {
        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool SawExchangeCancelled { get; private set; }

        public async Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken)
        {
            var reading = stream.ReadBodyAsync(cancellationToken);
            Waiting.SetResult();
            try
            {
                while (await reading is not null)
                {
                    reading = stream.ReadBodyAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (stream.IsExchangeCancelled)
            {
                SawExchangeCancelled = true;
                await (farewellReset
                    ? stream.WriteFarewellResetAsync(Http2ErrorCode.Cancel)
                    : stream.WriteFarewellTrailersAsync([new HpackHeaderField(":status", "200"), new HpackHeaderField("grpc-status", "14")]));
            }
        }
    }

    private sealed class TrailersHandler : IHttp2StreamHandler
    {
        public string? Path { get; private set; }

        public async Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken)
        {
            Path = stream.Head.Target;
            await stream.WriteHeadersAsync([new HpackHeaderField(":status", "200")], endStream: false);
            await stream.WriteDataAsync(Http2TestBytes.Ascii("payload"), endStream: false);
            await stream.WriteTrailersAsync([new HpackHeaderField("grpc-status", "0")]);
        }
    }
}
