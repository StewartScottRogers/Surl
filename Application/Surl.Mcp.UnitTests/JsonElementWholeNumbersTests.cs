using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-2065: every integer argument is read in each form the input schema counts as an integer.
[TestClass]
public sealed class JsonElementWholeNumbersTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("1", 1)]
    [DataRow("1.0", 1)]
    [DataRow("6e4", 60000)]
    [DataRow("-1.0", -1)]
    [DataRow("2147483647", int.MaxValue)]
    public void GetWholeInt32_AWholeNumberInAnyForm_IsRead(string json, int expected) =>
        Assert.AreEqual(expected, McpTestHost.Arguments(json).GetWholeInt32());

    [TestMethod]
    [DataRow("1.0", 1L)]
    [DataRow("6e4", 60000L)]
    [DataRow("9007199254740991", JsonElementWholeNumbers.LargestExactWholeNumber)]
    public void GetWholeInt64_AWholeNumberInAnyForm_IsRead(string json, long expected) =>
        Assert.AreEqual(expected, McpTestHost.Arguments(json).GetWholeInt64());

    [TestMethod]
    [DataRow(BrowseQueueTool.Name, """{"server_id": "s1", "queue": "orders", "after_position": @}""")]
    [DataRow(BrowseQueueTool.Name, """{"server_id": "s1", "queue": "orders", "limit": @}""")]
    [DataRow(GetEventsTool.Name, """{"server_id": "s1", "after_sequence": @}""")]
    [DataRow(GetEventsTool.Name, """{"server_id": "s1", "max_events": @}""")]
    [DataRow(GetExchangesTool.Name, """{"server_id": "s1", "after_exchange_id": @}""")]
    [DataRow(GetExchangesTool.Name, """{"server_id": "s1", "max_exchanges": @}""")]
    [DataRow(ListFilesTool.Name, """{"server_id": "s1", "path": "/", "limit": @}""")]
    [DataRow(ListKeysTool.Name, """{"server_id": "s1", "database": 1, "limit": @}""")]
    [DataRow(ListKeysTool.Name, """{"server_id": "s1", "database": @}""")]
    [DataRow(ListMessagesTool.Name, """{"server_id": "s1", "owner": "alice", "mailbox": "INBOX", "after_uid": @}""")]
    [DataRow(ListMessagesTool.Name, """{"server_id": "s1", "owner": "alice", "mailbox": "INBOX", "limit": @}""")]
    [DataRow(ReadKeyTool.Name, """{"server_id": "s1", "key": "a", "database": @}""")]
    [DataRow(ReadLogTool.Name, """{"server_id": "s1", "topic": "t/1", "from_offset": @}""")]
    [DataRow(ReadLogTool.Name, """{"server_id": "s1", "topic": "t/1", "limit": @}""")]
    [DataRow(ReadMessageTool.Name, """{"server_id": "s1", "owner": "alice", "mailbox": "INBOX", "uid": @}""")]
    [DataRow(WaitForEventTool.Name, """{"server_id": "s1", "timeout_ms": 30000, "after_sequence": @}""")]
    public async Task EveryToolsIntegerArgument_WrittenAsOnePointZero_IsReadAsOne(string toolName, string arguments)
    {
        await using var host = await StartSeededHostAsync();
        var tool = FindTool(host, toolName);

        var asOne = await CallAsync(tool, arguments.Replace("@", "1", StringComparison.Ordinal));
        var asOnePointZero = await CallAsync(tool, arguments.Replace("@", "1.0", StringComparison.Ordinal));

        Assert.IsFalse(asOne.IsError, asOne.TextContent.FirstOrDefault());
        Assert.IsFalse(asOnePointZero.IsError, asOnePointZero.TextContent.FirstOrDefault());
        Assert.AreEqual(Assert.ContainsSingle(asOne.TextContent), Assert.ContainsSingle(asOnePointZero.TextContent));
    }

    [TestMethod]
    public async Task WaitForEventsTimeout_WrittenAsOnePointZero_IsOneMillisecond()
    {
        var clock = new ManualTimeProvider();
        await using var host = new McpTestHost(timeProvider: clock);
        await CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""");

        var waiting = CallAsync(host.WaitForEvent, """{"server_id": "s1", "kinds": ["hook_failed"], "timeout_ms": 1.0}""");
        Assert.IsFalse(waiting.IsFaulted, waiting.Exception?.Message);
        await clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(1), TestContext.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual("""{"timed_out":true}""", Assert.ContainsSingle((await waiting).TextContent));
    }

    private static McpTool FindTool(McpTestHost host, string name) =>
        new[]
        {
            host.BrowseQueue, host.GetEvents, host.GetExchanges, host.ListFiles, host.ListKeys, host.ListMessages,
            host.ReadKey, host.ReadLog, host.ReadMessage, host.WaitForEvent,
        }.Single(tool => tool.Name == name);

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);

    private async Task SucceedAsync(McpTool tool, string arguments)
    {
        var result = await CallAsync(tool, arguments);
        Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
    }

    // One server holding two or more of everything the tools page through, so 1 and the default answer differently.
    private async Task<McpTestHost> StartSeededHostAsync()
    {
        var host = new McpTestHost(areExtensionsTurnedOn: true);
        await SucceedAsync(
            host.StartServer,
            """{"listen_urls": ["pop3://127.0.0.1:0", "http://127.0.0.1:0", "mqtt://127.0.0.1:0", "redis://127.0.0.1:0"], "arguments": ["--user", "alice:secret", "--allow-plaintext-auth", "--queue", "orders"]}""");
        foreach (var text in new[] { "first", "second", "third" })
        {
            await SucceedAsync(host.SeedMail, $$"""{"server_id": "s1", "to": "alice@x", "message": "Subject: {{text}}\r\n\r\n{{text}}\r\n"}""");
            await SucceedAsync(host.PutFile, $$"""{"server_id": "s1", "path": "/{{text}}.txt", "text": "{{text}}"}""");
            await SucceedAsync(host.PublishMessage, $$"""{"server_id": "s1", "queue": "orders", "payload": "{{text}}"}""");
            await SucceedAsync(host.PublishMessage, $$"""{"server_id": "s1", "topic": "t/1", "payload": "{{text}}"}""");
        }

        await SucceedAsync(host.SetKey, """{"server_id": "s1", "key": "a", "value": "zero"}""");
        await SucceedAsync(host.SetKey, """{"server_id": "s1", "key": "a", "value": "one", "database": 1}""");
        await SucceedAsync(host.SetKey, """{"server_id": "s1", "key": "b", "value": "two", "database": 1}""");
        for (var client = 0; client < 2; client++)
        {
            var connection = new FakeConnection("QUIT\r\n"u8.ToArray());
            host.Seams.ListenerFactory.Accept("redis", connection);
            await connection.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        }

        return host;
    }
}
