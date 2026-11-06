namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpEchoToolTests
{
    [TestMethod]
    public async Task Call_Text_ReturnsItUnchangedInThePagesTextShape()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();

        string response = await harness.CallAsync("echo", """{"text":"hello\nworld"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"hello\nworld"}],"isError":false}}""", response);
    }

    [TestMethod]
    public async Task Call_NoText_IsTheCoresInvalidParams()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();

        string response = await harness.CallAsync("echo", "{}");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"Invalid params: arguments.text: required"}}""", response);
    }
}
