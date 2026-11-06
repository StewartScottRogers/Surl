using System.Text;
using Surl.Api;

namespace Surl.Mcp;

[TestClass]
public sealed class WaitForEventToolTests
{
    // The request head upstream curl 8.21.0 sends for a plain GET (Host, User-Agent, Accept).
    private static readonly byte[] CurlGetHello = Encoding.ASCII.GetBytes(
        $"GET /hello.txt HTTP/1.1\r\nHost: 127.0.0.1:{FakeListenerFactory.BoundPort}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Handler_AFakeClientsGet_ReturnsTheFirstMatchingEventSkippingKindsNotAskedFor()
    {
        var connection = new FakeConnection(CurlGetHello);
        await using var host = new McpTestHost(listenerFactory: new FakeListenerFactory { ConnectionToAccept = connection });
        await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"], "files": {"hello.txt": "hello"}}""", TestContext.CancellationToken);

        var result = await McpTestHost.CallAsync(
            host.WaitForEvent,
            """{"server_id": "s1", "kinds": ["exchange_ended"], "scheme": "http", "timeout_ms": 30000}""",
            TestContext.CancellationToken);

        Assert.IsFalse(result.IsError);
        var structured = result.StructuredContent!.Value;
        var surlEvent = structured.GetProperty("event");
        Assert.AreEqual("exchange_ended", surlEvent.GetProperty("kind").GetString());
        Assert.AreEqual(CurlGetHello.Length, surlEvent.GetProperty("received_byte_count").GetInt64());
        Assert.AreEqual(connection.WrittenToClient.Length, surlEvent.GetProperty("sent_byte_count").GetInt64());
        Assert.IsFalse(structured.TryGetProperty("timed_out", out _));
        Assert.AreEqual(structured.GetRawText(), Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_NoMatchingEventBeforeTheTimeoutOnTheHostsClock_ReturnsTimedOutAndIsNoError()
    {
        var clock = new ManualTimeProvider();
        await using var host = new McpTestHost(timeProvider: clock);
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        var waiting = McpTestHost.CallAsync(
            host.WaitForEvent, """{"server_id": "s1", "kinds": ["hook_failed"], "timeout_ms": 1500}""", TestContext.CancellationToken);
        await clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(1500), TestContext.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(1500));
        var result = await waiting;

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("""{"timed_out":true}""", Assert.ContainsSingle(result.TextContent));
        Assert.IsTrue(result.StructuredContent!.Value.GetProperty("timed_out").GetBoolean());
    }

    [TestMethod]
    public async Task Handler_NoTimeoutGiven_WaitsTheDefaultTenSeconds()
    {
        var clock = new ManualTimeProvider();
        await using var host = new McpTestHost(timeProvider: clock);
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        var waiting = McpTestHost.CallAsync(host.WaitForEvent, """{"server_id": "s1", "scheme": "ftp"}""", TestContext.CancellationToken);
        await clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(WaitForEventTool.DefaultTimeoutMilliseconds), TestContext.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(WaitForEventTool.DefaultTimeoutMilliseconds));

        Assert.IsTrue((await waiting).StructuredContent!.Value.GetProperty("timed_out").GetBoolean());
    }

    [TestMethod]
    public async Task Handler_CallerCancels_ThrowsOperationCanceledRatherThanTimingOut()
    {
        await using var host = new McpTestHost();
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => McpTestHost.CallAsync(host.WaitForEvent, """{"server_id": "s1", "kinds": ["hook_failed"]}""", cancelled.Token));
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    [DataRow(60001L)]
    [DataRow(long.MaxValue)]
    public async Task Handler_TimeoutOutOfRange_IsAnErrorNamingTheRange(long timeoutMilliseconds)
    {
        await using var host = new McpTestHost();
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        var result = await McpTestHost.CallAsync(
            host.WaitForEvent, $$"""{"server_id": "s1", "timeout_ms": {{timeoutMilliseconds}}}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual($"timeout_ms must be 1 to 60000, not {timeoutMilliseconds}", Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_UnknownServerId_IsAnErrorNamingTheId()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(host.WaitForEvent, """{"server_id": "s9"}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("no running server has ID s9", Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_ListenUrlThatIsNotAnAbsoluteUrl_IsAnErrorNamingIt()
    {
        await using var host = new McpTestHost();
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        var result = await McpTestHost.CallAsync(host.WaitForEvent, """{"server_id": "s1", "listen_url": "not a url"}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("listen_url is not an absolute URL: not a url", Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public void Create_NullServers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => WaitForEventTool.Create(null!));
}
