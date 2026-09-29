namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpConnectionPersistenceTests
{
    [TestMethod]
    [DataRow(1, "", "", true)]
    [DataRow(0, "", "", false)]
    [DataRow(0, "Connection", "keep-alive", true)]
    [DataRow(0, "Connection", "foo , KEEP-ALIVE", true)]
    [DataRow(1, "Connection", "close", false)]
    [DataRow(1, "Connection", "\tClose ", false)]
    [DataRow(0, "Connection", "keep-alive, close", false)]
    [DataRow(1, "Content-Length", "0", true)]
    [DataRow(1, "Content-Length", "10", true)]
    [DataRow(0, "Content-Length", "0", false)]
    [DataRow(1, "Transfer-Encoding", "chunked", true)]
    public void KeepsConnectionOpen_DecidesByVersionAndConnectionNotByBody(int minorVersion, string name, string value, bool expected)
    {
        HttpRequestField[] fields = name.Length == 0 ? [] : [new HttpRequestField(name, value)];
        var head = new HttpRequestHead("GET", "/", new Version(1, minorVersion), fields);

        Assert.AreEqual(expected, HttpConnectionPersistence.KeepsConnectionOpen(head));
    }

    [TestMethod]
    [DataRow(false, 1, "close")]
    [DataRow(false, 0, "close")]
    [DataRow(true, 0, "keep-alive")]
    [DataRow(true, 1, null)]
    public void ConnectionFieldValue_SaysOnlyWhatDiffersFromTheVersionsDefault(bool keepsOpen, int minorVersion, string? expected)
    {
        Assert.AreEqual(expected, HttpConnectionPersistence.ConnectionFieldValue(keepsOpen, new Version(1, minorVersion)));
    }
}
