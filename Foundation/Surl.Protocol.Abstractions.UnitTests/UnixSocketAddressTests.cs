namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class UnixSocketAddressTests
{
    [TestMethod]
    public void Constructor_SocketFilePath_KeepsTheNameAndIsNotAbstract()
    {
        var address = new UnixSocketAddress("/tmp/surl.sock", isAbstract: false);

        Assert.AreEqual("/tmp/surl.sock", address.Name);
        Assert.IsFalse(address.IsAbstract);
    }

    [TestMethod]
    public void UnixUrl_SocketFilePath_IsUnixAndThePath()
    {
        var address = new UnixSocketAddress("/tmp/surl.sock", isAbstract: false);

        Assert.AreEqual("unix:///tmp/surl.sock", address.UnixUrl);
    }

    [TestMethod]
    public void UnixUrl_AbstractName_ShowsTheLeadingNulAsAnAt()
    {
        var address = new UnixSocketAddress("surl-test", isAbstract: true);

        Assert.IsTrue(address.IsAbstract);
        Assert.AreEqual("unix://@surl-test", address.UnixUrl);
    }

    [TestMethod]
    public void Equals_SameNameAndKind_AreEqual()
    {
        Assert.AreEqual(new UnixSocketAddress("a.sock", false), new UnixSocketAddress("a.sock", false));
        Assert.AreNotEqual(new UnixSocketAddress("a.sock", false), new UnixSocketAddress("a.sock", true));
    }

    [TestMethod]
    public void Constructor_EmptyName_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new UnixSocketAddress(string.Empty, false));
    }

    [TestMethod]
    public void Constructor_NullName_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UnixSocketAddress(null!, false));
    }
}
