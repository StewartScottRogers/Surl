using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Control.ControlHarness;

namespace Surl.Control;

[TestClass]
public sealed class ControlEndpointTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullTargetOrToken_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ControlEndpoint(null!, new ControlToken(Token)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ControlEndpoint(new FakeControlTarget(), null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullConnectionOrContext_Throws()
    {
        var endpoint = Endpoint(new FakeControlTarget());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => endpoint.ServeAsync(null!, Context(TimeProvider.System, LoopbackPeer, TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => endpoint.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task ServeAsync_BodyAnnouncedPastSixteenMebibytes_IsRefused413WithoutReadingIt()
    {
        var target = new FakeControlTarget();
        var request = $"PUT /v1/files/big HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {Token}\r\nContent-Length: {ControlEndpoint.MaxBodyBytes + 1}\r\n\r\n";

        var response = await ServeAsync(Endpoint(target), request, new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual("HTTP/1.1 413 Content Too Large", StatusLine(response));
        StringAssert.Contains(response, "Connection: close\r\n");
        Assert.AreEqual("{\"error\":\"too_large\",\"message\":\"a request body is at most 16777216 bytes\"}", Body(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedBodyPastSixteenMebibytes_IsRefused413()
    {
        var target = new FakeControlTarget();
        var head = $"PUT /v1/files/big HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {Token}\r\nTransfer-Encoding: chunked\r\n\r\n{ControlEndpoint.MaxBodyBytes + 1:x}\r\n";
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes(head), new byte[ControlEndpoint.MaxBodyBytes + 1], Encoding.ASCII.GetBytes("\r\n0\r\n\r\n")]);

        await Endpoint(target).ServeAsync(connection, Context(new ManualTimeProvider(), LoopbackPeer, TestContext.CancellationToken));

        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.WrittenBytes), "HTTP/1.1 413 Content Too Large");
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_BodyOfSixteenMebibytes_IsWritten()
    {
        var target = new FakeControlTarget();
        var head = $"PUT /v1/files/big HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {Token}\r\nContent-Length: {ControlEndpoint.MaxBodyBytes}\r\nConnection: close\r\n\r\n";
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes(head), new byte[ControlEndpoint.MaxBodyBytes]]);

        await Endpoint(target).ServeAsync(connection, Context(new ManualTimeProvider(), LoopbackPeer, TestContext.CancellationToken));

        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.WrittenBytes), "HTTP/1.1 204 No Content");
        Assert.HasCount(1, target.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_BodyWithATransferCodingItCannotRead_Is400AndCloses()
    {
        var request = $"PUT /v1/files/a HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {Token}\r\nTransfer-Encoding: gzip\r\n\r\n";

        var response = await ServeAsync(Endpoint(new FakeControlTarget()), request, new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(response));
        StringAssert.Contains(response, "Connection: close\r\n");
        Assert.AreEqual("{\"error\":\"bad_request\",\"message\":\"the request body\\u0027s transfer coding cannot be read\"}", Body(response));
    }

    [TestMethod]
    public async Task ServeAsync_BodyEndingBeforeItsLength_Is400AndCloses()
    {
        var request = $"PUT /v1/files/a HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {Token}\r\nContent-Length: 10\r\n\r\nabc";

        var response = await ServeAsync(Endpoint(new FakeControlTarget()), request, new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual("{\"error\":\"bad_request\",\"message\":\"the request body ended before it was complete\"}", Body(response));
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedBody_IsReadWhole()
    {
        var target = new FakeControlTarget();
        var request = $"PUT /v1/files/a HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {Token}\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n3\r\nabc\r\n0\r\n\r\n";

        var response = await ServeAsync(Endpoint(target), request, new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "WriteFile /a abc" }, target.Calls);
    }

    [TestMethod]
    public async Task FeedEventsAsync_TargetsEvents_AreHeldUntilTheFeedIsCancelled()
    {
        var target = new FakeControlTarget();
        var endpoint = Endpoint(target);
        using var feedStop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var feeding = endpoint.FeedEventsAsync(feedStop.Token);
        await target.Events.Writer.WriteAsync(new HookFailedEvent("h1", HookFailure.Threw, "x") { Sequence = 1 }, TestContext.CancellationToken);

        var response = await ServeAsync(endpoint, Request("GET", "/v1/events/wait?timeout_ms=60000"), TimeProvider.System, TestContext.CancellationToken);
        await feedStop.CancelAsync();
        await feeding;

        StringAssert.Contains(Body(response), "\"kind\":\"hook_failed\"");
        Assert.IsTrue(feeding.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task FeedEventsAsync_TargetEndsItsEvents_Returns()
    {
        var target = new FakeControlTarget();
        target.Events.Writer.Complete();

        await Endpoint(target).FeedEventsAsync(TestContext.CancellationToken);

        Assert.IsTrue(target.Events.Reader.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task FeedEventsAsync_TargetFailsWithoutCancellation_LetsTheFailureThrough()
    {
        var target = new FakeControlTarget();
        target.Events.Writer.Complete(new OperationCanceledException("not ours"));

        await Assert.ThrowsAsync<OperationCanceledException>(() => Endpoint(target).FeedEventsAsync(TestContext.CancellationToken));
    }
}
