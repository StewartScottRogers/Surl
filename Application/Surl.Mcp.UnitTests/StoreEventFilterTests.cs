using System.Text.Json;
using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

// BL-1202: get_events and wait_for_event filter by store and by a store change's name, as Surl.Api's SurlEventQuery does.
[TestClass]
public sealed class StoreEventFilterTests
{
    private const string StartMailAndFiles =
        """{"listen_urls": ["pop3://127.0.0.1:0"], "arguments": ["--user", "alice:secret", "--allow-plaintext-auth"]}""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetEvents_StoreContent_ReturnsOnlyTheFilesChange()
    {
        await using var host = await SeededHostAsync();

        var all = await McpTestHost.CallAsync(
            host.GetEvents, """{"server_id": "s1", "kinds": ["store_changed"]}""", TestContext.CancellationToken);
        var content = await McpTestHost.CallAsync(
            host.GetEvents, """{"server_id": "s1", "store": "content"}""", TestContext.CancellationToken);

        Assert.IsGreaterThan(1, all.StructuredContent!.Value.GetProperty("events").GetArrayLength());
        var change = Assert.ContainsSingle(content.StructuredContent!.Value.GetProperty("events").EnumerateArray());
        Assert.AreEqual("content", change.GetProperty("store").GetString());
        Assert.AreEqual("/seeded.txt", change.GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task WaitForEvent_NameOfOneSeededPath_ReturnsThatChange()
    {
        await using var host = await SeededHostAsync();

        var result = await McpTestHost.CallAsync(
            host.WaitForEvent, """{"server_id": "s1", "name": "/seeded.txt", "timeout_ms": 30000}""", TestContext.CancellationToken);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("/seeded.txt", result.StructuredContent!.Value.GetProperty("event").GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task WaitForEvent_NameNothingTouched_TimesOut()
    {
        var clock = new ManualTimeProvider();
        await using var host = await SeededHostAsync(clock);

        var waiting = McpTestHost.CallAsync(
            host.WaitForEvent, """{"server_id": "s1", "name": "/untouched", "timeout_ms": 1500}""", TestContext.CancellationToken);
        await clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(1500), TestContext.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(1500));
        var result = await waiting;

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("""{"timed_out":true}""", Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public void Schema_StoreEnum_ListsEveryStoreKindInSnakeCase()
    {
        using var schema = JsonDocument.Parse(GetEventsTool.InputSchema);
        var listed = schema.RootElement.GetProperty("properties").GetProperty("store").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()).ToArray();

        CollectionAssert.AreEqual(
            Enum.GetValues<StoreKind>().Select(kind => JsonNamingPolicy.SnakeCaseLower.ConvertName(kind.ToString())).ToArray(), listed);
    }

    private async Task<McpTestHost> SeededHostAsync(TimeProvider? clock = null)
    {
        var host = new McpTestHost(timeProvider: clock);
        var started = await McpTestHost.CallAsync(host.StartServer, StartMailAndFiles, TestContext.CancellationToken);
        Assert.IsFalse(started.IsError, string.Join(Environment.NewLine, started.TextContent));
        var mail = await McpTestHost.CallAsync(
            host.SeedMail, """{"server_id": "s1", "to": "alice@x", "message": "Subject: hi\r\n\r\nhello\r\n"}""", TestContext.CancellationToken);
        Assert.IsFalse(mail.IsError, string.Join(Environment.NewLine, mail.TextContent));
        var file = await McpTestHost.CallAsync(
            host.PutFile, """{"server_id": "s1", "path": "/seeded.txt", "text": "x"}""", TestContext.CancellationToken);
        Assert.IsFalse(file.IsError, string.Join(Environment.NewLine, file.TextContent));
        return host;
    }
}
