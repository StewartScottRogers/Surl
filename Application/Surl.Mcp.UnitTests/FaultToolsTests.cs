using System.Text;
using Surl.Api;
using Surl.Cli;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-812: add_fault and remove_fault (ADR-0124 decision 8), proved by fake HTTP clients.
[TestClass]
public sealed class FaultToolsTests
{
    private const string HttpGet = "GET /hello.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n";

    private static readonly TimeSpan ExchangeTimeLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AddFault_ThenAFakeRequest_IsDroppedAndAfterRemoveFaultTheNextIsServed()
    {
        await using var host = await StartHttpHostAsync();

        var added = await CallAsync(host.AddFault, """{"server_id": "s1", "fault": "drop;scheme=http"}""");
        var dropped = await ServeAsync(host);
        var removed = await CallAsync(host.RemoveFault, """{"server_id": "s1", "fault_id": "f1"}""");
        var served = await ServeAsync(host);
        var applied = await CallAsync(host.GetEvents, """{"server_id": "s1", "kinds": ["fault_applied"]}""");

        StoreToolsTests.AssertResult(added, """{"fault_id":"f1"}""");
        StoreToolsTests.AssertResult(removed, """{"fault_id":"f1"}""");
        Assert.IsEmpty(dropped);
        StringAssert.StartsWith(Encoding.ASCII.GetString(served), "HTTP/1.1 200 OK\r\n");
        Assert.HasCount(1, applied.StructuredContent!.Value.GetProperty("events").EnumerateArray());
    }

    [TestMethod]
    [DataRow("bogus", "option --fault: unknown kind bogus")]
    [DataRow("error;reply=503 Busy", "option --fault: kind error needs key scheme or key listener")]
    public async Task AddFault_ABadItem_IsAnErrorWithSurlsText(string item, string expected)
    {
        await using var host = await StartHttpHostAsync();

        var result = await CallAsync(host.AddFault, $$"""{"server_id": "s1", "fault": "{{item}}"}""");

        StoreToolsTests.AssertFailure(result, expected);
    }

    [TestMethod]
    public async Task AddFault_PastTheRuleLimit_IsAnErrorWithThePlansText()
    {
        await using var host = await StartHttpHostAsync();
        for (var rule = 0; rule < 256; rule++)
        {
            await CallAsync(host.AddFault, """{"server_id": "s1", "fault": "refuse-login"}""");
        }

        var result = await CallAsync(host.AddFault, """{"server_id": "s1", "fault": "refuse-login"}""");

        StoreToolsTests.AssertFailure(result, "A fault plan holds at most 256 rules.");
    }

    [TestMethod]
    [DataRow("delay;ms=250;stage=command;path=/slow;times=2")]
    [DataRow("drop;listener=http://127.0.0.1:0;after-bytes=10")]
    [DataRow("refuse-login;account=alice")]
    [DataRow("error;scheme=http;command=GET;reply=503 Busy")]
    [DataRow("out-of-space;path=/uploads")]
    public void ToSurlFault_EachKind_SpellsTheSameItem(string item)
    {
        var configured = CommandLineParser.Parse(["http://127.0.0.1:0", "--fault", item]).CommandLine!.Faults[0];

        var fault = AddFaultTool.ToSurlFault(configured);

        var reread = CommandLineParser.Parse(["http://127.0.0.1:0", "--fault", fault.ToItem()]).CommandLine!.Faults[0];
        Assert.AreEqual(configured, reread);
        Assert.AreEqual(configured.Kind, fault.Kind);
    }

    [TestMethod]
    public async Task RemoveFault_AnIdThePlanDoesNotHold_IsAnError()
    {
        await using var host = await StartHttpHostAsync();

        var result = await CallAsync(host.RemoveFault, """{"server_id": "s1", "fault_id": "f7"}""");

        StoreToolsTests.AssertFailure(result, "server s1 has no fault rule f7");
    }

    private async Task<McpTestHost> StartHttpHostAsync()
    {
        var host = new McpTestHost();
        var started = await CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"], "files": {"hello.txt": "hello"}}""");
        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        return host;
    }

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);

    private async Task<byte[]> ServeAsync(McpTestHost host)
    {
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(HttpGet));
        host.Seams.ListenerFactory.Accept("http", connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return connection.WrittenToClient;
    }
}
