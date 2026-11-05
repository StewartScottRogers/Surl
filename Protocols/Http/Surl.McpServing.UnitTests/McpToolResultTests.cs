using System.Text.Json;

namespace Surl.McpServing;

[TestClass]
public sealed class McpToolResultTests
{
    [TestMethod]
    public void FromText_Text_IsOneBlockAndNotAnError()
    {
        McpToolResult result = McpToolResult.FromText("ok");

        CollectionAssert.AreEqual(new[] { "ok" }, result.TextContent.ToArray());
        Assert.IsFalse(result.IsError);
        Assert.IsNull(result.StructuredContent);
    }

    [TestMethod]
    public void FromFailure_Message_IsOneBlockAndAnError()
    {
        McpToolResult result = McpToolResult.FromFailure("no");

        CollectionAssert.AreEqual(new[] { "no" }, result.TextContent.ToArray());
        Assert.IsTrue(result.IsError);
    }

    [TestMethod]
    public void Constructor_StructuredContentThatIsNotAnObject_Throws()
    {
        JsonElement array = JsonDocument.Parse("[]").RootElement;

        Assert.ThrowsExactly<ArgumentException>(() => new McpToolResult(["x"], array));
    }

    [TestMethod]
    public void Constructor_NullTextOrNullBlock_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpToolResult(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpToolResult([null!]));
    }
}
