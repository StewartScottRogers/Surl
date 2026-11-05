namespace Surl.McpServing;

[TestClass]
public sealed class McpSessionOptionsTests
{
    private static readonly McpServerInfo Server = new("s", null, "1");

    [TestMethod]
    public void Constructor_Defaults_AreFourMebibytesAHundredPerPageAndNoInstructions()
    {
        McpSessionOptions options = new(Server);

        Assert.AreEqual(4 * 1024 * 1024, options.MaximumMessageBytes);
        Assert.AreEqual(100, options.ListPageSize);
        Assert.IsNull(options.Instructions);
        Assert.AreSame(Server, options.ServerInfo);
    }

    [TestMethod]
    public void Constructor_LimitOrPageSizeBelowOne_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new McpSessionOptions(Server, maximumMessageBytes: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new McpSessionOptions(Server, listPageSize: 0));
    }

    [TestMethod]
    public void Constructor_NullServerInfo_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpSessionOptions(null!));
    }
}
