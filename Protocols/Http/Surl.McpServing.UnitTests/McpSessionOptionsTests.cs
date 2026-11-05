namespace Surl.McpServing;

[TestClass]
public sealed class McpSessionOptionsTests
{
    private static readonly McpServerInfo Server = new("s", null, "1");

    private static readonly McpServerCapabilities None = new(false, false);

    [TestMethod]
    public void Constructor_Defaults_AreFourMebibytesAndNoInstructions()
    {
        McpSessionOptions options = new(Server, None);

        Assert.AreEqual(4 * 1024 * 1024, options.MaximumMessageBytes);
        Assert.IsNull(options.Instructions);
        Assert.AreSame(Server, options.ServerInfo);
        Assert.AreSame(None, options.Capabilities);
    }

    [TestMethod]
    public void Constructor_LimitBelowOne_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new McpSessionOptions(Server, None, maximumMessageBytes: 0));
    }

    [TestMethod]
    public void Constructor_NullServerInfoOrCapabilities_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpSessionOptions(null!, None));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpSessionOptions(Server, null!));
    }
}
