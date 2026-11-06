namespace Surl.McpServing;

[TestClass]
public sealed class McpEmbeddedResourceTests
{
    [TestMethod]
    public void Constructor_CopiesTheBytes()
    {
        byte[] bytes = [1, 2, 3];

        McpEmbeddedResource resource = new("file:///a", "application/octet-stream", bytes);
        bytes[0] = 9;

        Assert.AreEqual("file:///a", resource.Uri);
        Assert.AreEqual("application/octet-stream", resource.MimeType);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, resource.Blob.ToArray());
    }

    [TestMethod]
    public void Constructor_MissingArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new McpEmbeddedResource("", "x", []));
        Assert.ThrowsExactly<ArgumentException>(() => new McpEmbeddedResource("u", "", []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpEmbeddedResource("u", "x", null!));
    }
}
