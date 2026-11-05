using System.Net;
using System.Text;
using System.Text.Json;
using Surl.Api;

namespace Surl.Mcp;

[TestClass]
public sealed class GetExchangesToolTests
{
    // The request head upstream curl 8.21.0 sends for a plain GET (Host, User-Agent, Accept, as
    // HttpConnectionReaderTests pins from the recorded exchange).
    private static readonly byte[] CurlGetHello = Encoding.ASCII.GetBytes(
        $"GET /hello.txt HTTP/1.1\r\nHost: 127.0.0.1:{FakeListenerFactory.BoundPort}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n");

    private static readonly IPEndPoint Client = new(IPAddress.Loopback, 50512);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Handler_AFakeClientsGet_ReturnsTheExchangeWithItsClientAndTheExactBytesReceivedAndSent()
    {
        var connection = new FakeConnection(CurlGetHello);
        await using var host = new McpTestHost(listenerFactory: new FakeListenerFactory { ConnectionToAccept = connection });
        await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"], "files": {"hello.txt": "hello"}}""", TestContext.CancellationToken);
        await connection.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        var result = await McpTestHost.CallAsync(host.GetExchanges, """{"server_id": "s1"}""", TestContext.CancellationToken);

        Assert.IsFalse(result.IsError);
        var structured = result.StructuredContent!.Value;
        Assert.AreEqual("s1", structured.GetProperty("server_id").GetString());
        var exchange = Assert.ContainsSingle(structured.GetProperty("exchanges").EnumerateArray());
        Assert.AreEqual(FakeConnection.Client.ToString(), exchange.GetProperty("client").GetString());
        Assert.AreEqual("2026-11-05T10:00:00.0000000+00:00", exchange.GetProperty("started_at").GetString());
        CollectionAssert.AreEqual(CurlGetHello, Bytes(exchange, "received"));
        CollectionAssert.AreEqual(connection.WrittenToClient, Bytes(exchange, "sent"));
        Assert.AreEqual(CurlGetHello.Length + connection.WrittenToClient.Length, exchange.GetProperty("recorded_bytes").GetInt32());
        Assert.AreEqual(exchange.GetProperty("recorded_bytes").GetInt32(), exchange.GetProperty("shown_bytes").GetInt32());
        Assert.IsFalse(exchange.GetProperty("is_cut").GetBoolean());
        Assert.IsFalse(exchange.GetProperty("is_truncated").GetBoolean());
        Assert.AreEqual(0, structured.GetProperty("dropped_exchange_count").GetInt64());
        Assert.IsFalse(structured.GetProperty("has_more").GetBoolean());
        Assert.AreEqual(structured.GetRawText(), Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_UnknownServerId_IsAnErrorNamingTheId()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(host.GetExchanges, """{"server_id": "s9"}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("no running server has ID s9", Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_RunningServerWithNoExchanges_ReturnsNone()
    {
        await using var host = new McpTestHost();
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        var result = await McpTestHost.CallAsync(host.GetExchanges, """{"server_id": "s1"}""", TestContext.CancellationToken);

        Assert.AreEqual(0, result.StructuredContent!.Value.GetProperty("exchanges").GetArrayLength());
    }

    [TestMethod]
    public void Answer_TwoExchanges_ReturnsThemInTheOrderTheyStarted()
    {
        var records = new[] { Record(3, "first"u8.ToArray()), Record(4, "second"u8.ToArray()) };

        var structured = GetExchangesTool.Answer("s1", records, 2, McpTestHost.Arguments("""{"server_id": "s1"}""")).StructuredContent!.Value;

        var exchanges = structured.GetProperty("exchanges").EnumerateArray().ToArray();
        CollectionAssert.AreEqual(new[] { 3L, 4L }, exchanges.Select(exchange => exchange.GetProperty("exchange_id").GetInt64()).ToArray());
        Assert.AreEqual("first", exchanges[0].GetProperty("chunks")[0].GetProperty("data").GetString());
        Assert.AreEqual("second", exchanges[1].GetProperty("chunks")[0].GetProperty("data").GetString());
        Assert.AreEqual(2, structured.GetProperty("dropped_exchange_count").GetInt64());
    }

    [TestMethod]
    public void Answer_AfterExchangeIdAndMaxExchanges_ReturnsOnlyLaterExchangesUpToTheMaxAndSaysThereIsMore()
    {
        var records = new[] { Record(1, "a"u8.ToArray()), Record(2, "b"u8.ToArray()), Record(3, "c"u8.ToArray()), Record(4, "d"u8.ToArray()) };

        var structured = GetExchangesTool.Answer(
            "s1", records, 0, McpTestHost.Arguments("""{"server_id": "s1", "after_exchange_id": 1, "max_exchanges": 2}""")).StructuredContent!.Value;

        CollectionAssert.AreEqual(
            new[] { 2L, 3L },
            structured.GetProperty("exchanges").EnumerateArray().Select(exchange => exchange.GetProperty("exchange_id").GetInt64()).ToArray());
        Assert.IsTrue(structured.GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    public void Answer_MoreExchangesThanTheDefaultMax_ReturnsTwenty()
    {
        var records = Enumerable.Range(1, 21).Select(id => Record(id, "x"u8.ToArray())).ToArray();

        var structured = GetExchangesTool.Answer("s1", records, 0, McpTestHost.Arguments("""{"server_id": "s1"}""")).StructuredContent!.Value;

        Assert.AreEqual(GetExchangesTool.DefaultMaxExchanges, structured.GetProperty("exchanges").GetArrayLength());
        Assert.IsTrue(structured.GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    public void Create_NullServers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GetExchangesTool.Create(null!));

    private static SurlExchangeRecord Record(long exchangeId, byte[] received) =>
        new(exchangeId, Client, McpTestHost.Now, [new SurlExchangeChunk(SurlExchangeDirection.Received, received)], IsTruncated: false);

    private static byte[] Bytes(JsonElement exchange, string direction) =>
        [.. exchange.GetProperty("chunks").EnumerateArray()
            .Where(chunk => chunk.GetProperty("direction").GetString() == direction)
            .SelectMany(chunk => chunk.GetProperty("encoding").GetString() == "text"
                ? Encoding.UTF8.GetBytes(chunk.GetProperty("data").GetString()!)
                : Convert.FromBase64String(chunk.GetProperty("data").GetString()!))];
}
