using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The MCP server hands its exchange to the content store (ADR-0168 decision 5), so a scoped
/// out-of-space rule matches a <c>write_file</c> call and its store change names the exchange.
/// </summary>
[TestClass]
public sealed class McpStoreExchangeTests
{
    private const string WriteCall = """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"write_file","arguments":{"path":"a.txt","text":"hello"}}}""";

    private static readonly ContentExposureOptions Uploads = new() { AllowUploads = true };

    [TestMethod]
    public async Task WriteFile_OutOfSpaceRuleForThisListener_RefusesTheWriteAndPublishesTheFault()
    {
        ScriptedFaultPlan faults = new(new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "mcp://127.0.0.1:8080/" });
        McpHttpHarness harness = new(exposure: Uploads, faults: faults);
        string sessionId = await harness.InitializeAsync();

        await harness.PostAsync(WriteCall, $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "a.txt")));
        Assert.AreEqual(1L, harness.Events.Events.OfType<FaultAppliedEvent>().Single().ExchangeId);
    }

    [TestMethod]
    public async Task WriteFile_OutOfSpaceRuleForAnotherListener_StoresAndPublishesTheChangeOnTheExchange()
    {
        ScriptedFaultPlan faults = new(new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "mcp://127.0.0.1:1/" });
        McpHttpHarness harness = new(exposure: Uploads, faults: faults);
        string sessionId = await harness.InitializeAsync();

        await harness.PostAsync(WriteCall, $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual(ContentEntryKind.File, harness.FileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "a.txt")));
        Assert.IsEmpty(harness.Events.Events.OfType<FaultAppliedEvent>());
        Assert.AreEqual(1L, harness.Events.Events.OfType<StoreChangedEvent>().Single(change => change.ToString()!.Contains("a.txt")).ExchangeId);
    }
}
