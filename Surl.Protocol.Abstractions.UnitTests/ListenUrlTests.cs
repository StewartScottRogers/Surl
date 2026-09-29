namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class ListenUrlTests
{
    [TestMethod]
    public void Equals_SameSchemeHostAndPort_AreEqual()
    {
        var first = new ListenUrl("http", "127.0.0.1", 8080);
        var second = new ListenUrl("http", "127.0.0.1", 8080);

        var equal = first == second;

        Assert.IsTrue(equal);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    [DataRow("https", "127.0.0.1", 8080)]
    [DataRow("http", "::1", 8080)]
    [DataRow("http", "127.0.0.1", 8081)]
    public void Equals_DifferentSchemeHostOrPort_AreNotEqual(string scheme, string host, int port)
    {
        var first = new ListenUrl("http", "127.0.0.1", 8080);
        var second = new ListenUrl(scheme, host, port);

        var equal = first == second;

        Assert.IsFalse(equal);
    }

    [TestMethod]
    public void BoundPort_NewListenUrl_IsNull()
    {
        var listenUrl = new ListenUrl("http", "localhost", 0);

        var boundPort = listenUrl.BoundPort;

        Assert.IsNull(boundPort);
    }

    [TestMethod]
    public void WithBoundPort_EphemeralPort_SetsBoundPortAndKeepsTheRest()
    {
        var listenUrl = new ListenUrl("http", "localhost", 0);

        var bound = listenUrl.WithBoundPort(49152);

        Assert.AreEqual(49152, bound.BoundPort);
        Assert.AreEqual("http", bound.Scheme);
        Assert.AreEqual("localhost", bound.Host);
        Assert.AreEqual(0, bound.Port);
        Assert.IsNull(listenUrl.BoundPort);
    }

    [TestMethod]
    public void Equals_BeforeAndAfterBinding_AreNotEqual()
    {
        var listenUrl = new ListenUrl("http", "127.0.0.1", 8080);

        var bound = listenUrl.WithBoundPort(8080);

        Assert.AreNotEqual(listenUrl, bound);
        Assert.AreEqual(bound, listenUrl.WithBoundPort(8080));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(65535)]
    public void WithBoundPort_PortAtTheEdgeOfTheRange_IsAccepted(int boundPort)
    {
        var listenUrl = new ListenUrl("http", "127.0.0.1", 0);

        var bound = listenUrl.WithBoundPort(boundPort);

        Assert.AreEqual(boundPort, bound.BoundPort);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(65536)]
    public void WithBoundPort_PortOutsideTheRange_Throws(int boundPort)
    {
        var listenUrl = new ListenUrl("http", "127.0.0.1", 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => listenUrl.WithBoundPort(boundPort));
    }
}
