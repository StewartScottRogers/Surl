namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The namespaces a listen URL's path names (ADR-0120, decision 4).
/// </summary>
[TestClass]
public sealed class SocketIoServedNamespacesTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("/")]
    [DataRow("//")]
    public void FromListenPath_EmptyOrSlash_ServesTheMainNamespaceOnly(string? listenPath)
    {
        var namespaces = SocketIoServedNamespaces.FromListenPath(listenPath);

        CollectionAssert.AreEqual(new[] { "/" }, namespaces.Names.ToArray());
    }

    [TestMethod]
    [DataRow("/admin")]
    [DataRow("/admin/")]
    [DataRow("admin")]
    public void FromListenPath_NamedPath_ServesTheMainNamespaceAndThePathWithoutItsTrailingSlash(string listenPath)
    {
        var namespaces = SocketIoServedNamespaces.FromListenPath(listenPath);

        CollectionAssert.AreEqual(new[] { "/", "/admin" }, namespaces.Names.ToArray());
        Assert.IsTrue(namespaces.Serves("/admin"));
        Assert.IsFalse(namespaces.Serves("/Admin"));
        Assert.IsFalse(namespaces.Serves("/admin/"));
    }
}
