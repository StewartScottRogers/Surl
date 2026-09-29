using System.Text;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpResponseHeadTests
{
    [TestMethod]
    public void Constructor_NullStatus_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpResponseHead(null!));
    }

    [TestMethod]
    public void ToBytes_WritesTheStatusLineAndFieldsInOrder_SkippingNullValues()
    {
        var head = new HttpResponseHead(HttpStatus.NotFound)
            .AddField("B", "2")
            .AddField("Skipped", null)
            .AddField("A", "1");

        Assert.AreSame(HttpStatus.NotFound, head.Status);
        Assert.AreEqual("HTTP/1.1 404 Not Found\r\nB: 2\r\nA: 1\r\n\r\n", Encoding.Latin1.GetString(head.ToBytes()));
    }
}
