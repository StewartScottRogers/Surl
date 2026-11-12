using System.Net;
using System.Text;
using System.Text.Json;
using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

[TestClass]
public sealed class GetEventsToolTests
{
    // The request head upstream curl 8.21.0 sends for a GET with -u alice:secret: Host, Authorization, User-Agent,
    // Accept, in curl's order.
    private static readonly byte[] CurlBasicGetHello = Encoding.ASCII.GetBytes(
        $"GET /hello.txt HTTP/1.1\r\nHost: 127.0.0.1:{FakeListenerFactory.BoundPort}\r\nAuthorization: Basic YWxpY2U6c2VjcmV0\r\n"
        + "User-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n");

    private const string StartWithAliceAndHello =
        """{"listen_urls": ["http://127.0.0.1:0"], "arguments": ["--allow-plaintext-auth", "-u", "alice:secret"], "files": {"hello.txt": "hello"}}""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Handler_AFakeClientsGetWithALogin_ReturnsTheExchangesStartLoginAndEndInOrder()
    {
        await using var host = await StartAfterOneGetAsync();

        var result = await McpTestHost.CallAsync(
            host.GetEvents, """{"server_id": "s1", "kinds": ["exchange_started", "login_judged", "exchange_ended"]}""", TestContext.CancellationToken);

        Assert.IsFalse(result.IsError);
        var structured = result.StructuredContent!.Value;
        var events = structured.GetProperty("events").EnumerateArray().ToArray();
        CollectionAssert.AreEqual(
            new[] { "exchange_started", "login_judged", "exchange_ended" }, events.Select(item => item.GetProperty("kind").GetString()).ToArray());
        Assert.AreEqual($"http://127.0.0.1:{FakeListenerFactory.BoundPort}/", events[0].GetProperty("listen_url").GetString());
        Assert.AreEqual("http", events[0].GetProperty("scheme").GetString());
        Assert.AreEqual(FakeConnection.Client.ToString(), events[0].GetProperty("client").GetString());
        Assert.AreEqual("alice", events[1].GetProperty("account").GetString());
        Assert.IsTrue(events[1].GetProperty("is_accepted").GetBoolean());
        Assert.IsFalse(events[1].GetRawText().Contains("secret", StringComparison.Ordinal));
        Assert.AreEqual(events[0].GetProperty("exchange_id").GetInt64(), events[2].GetProperty("exchange_id").GetInt64());
        Assert.IsTrue(events[0].GetProperty("sequence").GetInt64() < events[2].GetProperty("sequence").GetInt64());
        Assert.AreEqual("2026-11-05T10:00:00.0000000+00:00", events[0].GetProperty("at").GetString());
        Assert.IsFalse(structured.GetProperty("has_more").GetBoolean());
        Assert.AreEqual(0, structured.GetProperty("dropped_event_count").GetInt64());
        Assert.AreEqual(structured.GetRawText(), Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    [DataRow("""{"server_id": "s1", "kinds": ["exchange_ended"]}""", new[] { "exchange_ended" })]
    [DataRow("""{"server_id": "s1", "kinds": ["hook_failed"]}""", new string[0])]
    [DataRow("""{"server_id": "s1", "kinds": ["exchange_started", "exchange_ended"], "scheme": "HTTP"}""", new[] { "exchange_started", "exchange_ended" })]
    [DataRow("""{"server_id": "s1", "scheme": "ftp"}""", new string[0])]
    [DataRow("""{"server_id": "s1", "kinds": ["exchange_ended"], "listen_url": "http://127.0.0.1:49731/"}""", new[] { "exchange_ended" })]
    [DataRow("""{"server_id": "s1", "listen_url": "http://127.0.0.1:1/"}""", new string[0])]
    [DataRow("""{"server_id": "s1", "kinds": ["login_judged"], "account": "alice"}""", new[] { "login_judged" })]
    [DataRow("""{"server_id": "s1", "account": "bob"}""", new string[0])]
    public async Task Handler_EachFilter_ReturnsOnlyMatchingEvents(string arguments, string[] expectedKinds)
    {
        await using var host = await StartAfterOneGetAsync();

        var result = await McpTestHost.CallAsync(host.GetEvents, arguments, TestContext.CancellationToken);

        Assert.IsFalse(result.IsError);
        CollectionAssert.AreEqual(expectedKinds, Kinds(result.StructuredContent!.Value));
    }

    [TestMethod]
    public async Task Handler_AfterSequence_ReturnsOnlyLaterEvents()
    {
        await using var host = await StartAfterOneGetAsync();
        var all = (await McpTestHost.CallAsync(host.GetEvents, """{"server_id": "s1"}""", TestContext.CancellationToken)).StructuredContent!.Value;
        var firstSequence = all.GetProperty("events")[0].GetProperty("sequence").GetInt64();

        var result = await McpTestHost.CallAsync(
            host.GetEvents, $$"""{"server_id": "s1", "after_sequence": {{firstSequence}}}""", TestContext.CancellationToken);

        var sequences = result.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(item => item.GetProperty("sequence").GetInt64()).ToArray();
        Assert.HasCount(all.GetProperty("events").GetArrayLength() - 1, sequences);
        Assert.IsTrue(sequences.All(sequence => sequence > firstSequence));
    }

    [TestMethod]
    public async Task Handler_UnknownServerId_IsAnErrorNamingTheId()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(host.GetEvents, """{"server_id": "s9"}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("no running server has ID s9", Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_ListenUrlThatIsNotAnAbsoluteUrl_IsAnErrorNamingIt()
    {
        await using var host = new McpTestHost();
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        var result = await McpTestHost.CallAsync(host.GetEvents, """{"server_id": "s1", "listen_url": "not a url"}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("listen_url is not an absolute URL: not a url", Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public void Answer_MoreEventsThanMaxEvents_ReturnsTheFirstAndSaysThereIsMore()
    {
        SurlEvent[] found = [Ended(1), Ended(2), Ended(3)];

        var structured = GetEventsTool.Answer(found, Filter(), 7, McpTestHost.Arguments("""{"server_id": "s1", "max_events": 2}""")).StructuredContent!.Value;

        CollectionAssert.AreEqual(
            new[] { 1L, 2L }, structured.GetProperty("events").EnumerateArray().Select(item => item.GetProperty("sequence").GetInt64()).ToArray());
        Assert.IsTrue(structured.GetProperty("has_more").GetBoolean());
        Assert.AreEqual(7, structured.GetProperty("dropped_event_count").GetInt64());
    }

    [TestMethod]
    public void Answer_MoreEventsThanTheDefaultMax_ReturnsAHundred()
    {
        var found = Enumerable.Range(1, 101).Select(sequence => (SurlEvent)Ended(sequence)).ToArray();

        var structured = GetEventsTool.Answer(found, Filter(), 0, McpTestHost.Arguments("""{"server_id": "s1"}""")).StructuredContent!.Value;

        Assert.AreEqual(GetEventsTool.DefaultMaxEvents, structured.GetProperty("events").GetArrayLength());
        Assert.IsTrue(structured.GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    public void Answer_KindsNamingConsumerGroupChanged_ReturnsOnlyThatKind()
    {
        const string arguments = """{"server_id": "s1", "kinds": ["consumer_group_changed"]}""";
        EventToolFilter.TryRead(McpTestHost.Arguments(arguments), out var filter);
        SurlEvent[] found =
        [
            Ended(1),
            new ConsumerGroupChangedEvent(5, "kafka", "g1", ConsumerGroupChange.MemberJoined, "m1", 2, "alice") { Sequence = 2 },
        ];

        var structured = GetEventsTool.Answer(found, filter!, 0, McpTestHost.Arguments(arguments)).StructuredContent!.Value;

        CollectionAssert.AreEqual(new[] { "consumer_group_changed" }, Kinds(structured));
        StringAssert.Contains(EventToolFilter.EventProperties, "\"consumer_group_changed\"");
    }

    [TestMethod]
    public void Create_NullServers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GetEventsTool.Create(null!));

    private static EventToolFilter Filter()
    {
        EventToolFilter.TryRead(McpTestHost.Arguments("{}"), out var filter);
        return filter!;
    }

    private static ExchangeEndedEvent Ended(long sequence) => new(sequence, 10, 20) { Sequence = sequence };

    private static string[] Kinds(JsonElement structured) =>
        [.. structured.GetProperty("events").EnumerateArray().Select(item => item.GetProperty("kind").GetString()!)];

    private async Task<McpTestHost> StartAfterOneGetAsync()
    {
        var connection = new FakeConnection(CurlBasicGetHello);
        var host = new McpTestHost(listenerFactory: new FakeListenerFactory { ConnectionToAccept = connection });
        var started = await McpTestHost.CallAsync(host.StartServer, StartWithAliceAndHello, TestContext.CancellationToken);
        Assert.IsFalse(started.IsError, string.Join(Environment.NewLine, started.TextContent));
        await connection.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        await McpTestHost.CallAsync(
            host.WaitForEvent, """{"server_id": "s1", "kinds": ["exchange_ended"], "timeout_ms": 30000}""", TestContext.CancellationToken);
        return host;
    }
}
