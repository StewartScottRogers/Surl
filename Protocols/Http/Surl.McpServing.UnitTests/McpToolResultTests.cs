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
    public void Constructor_EmbeddedResources_KeepsThemAfterTheText()
    {
        McpEmbeddedResource resource = new("file:///a.bin", "application/octet-stream", [1, 2]);

        McpToolResult result = new(["t"], [resource], isError: true);

        Assert.AreSame(resource, result.EmbeddedResources.Single());
        CollectionAssert.AreEqual(new[] { "t" }, result.TextContent.ToArray());
        Assert.IsTrue(result.IsError);
    }

    [TestMethod]
    public void Constructor_TextOnly_HasNoEmbeddedResources() =>
        Assert.AreEqual(0, McpToolResult.FromText("x").EmbeddedResources.Count);

    [TestMethod]
    public void Constructor_NullResourcesOrNullResource_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpToolResult([], (IReadOnlyList<McpEmbeddedResource>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpToolResult([], [null!]));
    }

    [TestMethod]
    public void Constructor_NullTextOrNullBlock_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpToolResult(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpToolResult([null!]));
    }
}
