namespace Surl.McpServing;

[TestClass]
public sealed class McpToolTests
{
    [TestMethod]
    public void Constructor_Arguments_AreKept()
    {
        Func<System.Text.Json.JsonElement, CancellationToken, Task<McpToolResult>> handler = (_, _) => Task.FromResult(McpToolResult.FromText("x"));

        McpTool tool = new("n", "T", "d", """{"type":"object"}""", handler);

        Assert.AreEqual("n", tool.Name);
        Assert.AreEqual("T", tool.Title);
        Assert.AreEqual("d", tool.Description);
        Assert.AreEqual("""{"type":"object"}""", tool.InputSchema);
        Assert.AreSame(handler, tool.Handler);
    }

    [TestMethod]
    public void Constructor_EmptyOrNullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new McpTool("", null, "d", "{}", (_, _) => Task.FromResult(McpToolResult.FromText("x"))));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpTool("n", null, null!, "{}", (_, _) => Task.FromResult(McpToolResult.FromText("x"))));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpTool("n", null, "d", null!, (_, _) => Task.FromResult(McpToolResult.FromText("x"))));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpTool("n", null, "d", "{}", null!));
    }
}
