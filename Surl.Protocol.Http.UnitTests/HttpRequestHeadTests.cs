namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpRequestHeadTests
{
    [TestMethod]
    public void GetFieldValues_RepeatedNameInAnyCase_ReturnsEveryValueInOrder()
    {
        var head = new HttpRequestHead("GET", "/", new Version(1, 1),
        [
            new HttpRequestField("Accept", "a"),
            new HttpRequestField("Host", "h"),
            new HttpRequestField("ACCEPT", "b"),
        ]);

        var values = head.GetFieldValues("accept");

        CollectionAssert.AreEqual(new[] { "a", "b" }, values.ToArray());
    }

    [TestMethod]
    public void GetFieldValues_AbsentName_ReturnsEmpty()
    {
        var head = new HttpRequestHead("GET", "/", new Version(1, 1), []);

        var values = head.GetFieldValues("Host");

        Assert.IsEmpty(values);
    }

    [TestMethod]
    public void GetFieldValues_NullName_Throws()
    {
        var head = new HttpRequestHead("GET", "/", new Version(1, 1), []);

        Assert.ThrowsExactly<ArgumentNullException>(() => head.GetFieldValues(null!));
    }

    [TestMethod]
    public void Constructor_KeepsItsArguments()
    {
        var version = new Version(1, 0);

        var head = new HttpRequestHead("HEAD", "/a?b", version, [new HttpRequestField("X", "y")]);

        Assert.AreEqual("HEAD", head.Method);
        Assert.AreEqual("/a?b", head.RequestTarget);
        Assert.AreSame(version, head.Version);
        Assert.HasCount(1, head.Fields);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var version = new Version(1, 1);

        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpRequestHead(null!, "/", version, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpRequestHead("GET", null!, version, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpRequestHead("GET", "/", null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpRequestHead("GET", "/", version, null!));
    }
}
