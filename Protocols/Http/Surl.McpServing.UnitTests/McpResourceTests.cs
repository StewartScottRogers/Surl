namespace Surl.McpServing;

[TestClass]
public sealed class McpResourceTests
{
    [TestMethod]
    public void Constructor_AllFields_KeepsThem()
    {
        McpResource resource = new("surl://a", "a", "A", "d", "text/plain");

        Assert.AreEqual("surl://a", resource.Uri);
        Assert.AreEqual("a", resource.Name);
        Assert.AreEqual("A", resource.Title);
        Assert.AreEqual("d", resource.Description);
        Assert.AreEqual("text/plain", resource.MimeType);
    }

    [TestMethod]
    [DataRow("", "a")]
    [DataRow("surl://a", "")]
    public void Constructor_EmptyUriOrName_Throws(string uri, string name)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new McpResource(uri, name, null, null, null));
    }
}
