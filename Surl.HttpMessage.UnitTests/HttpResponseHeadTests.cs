using System.Text;

namespace Surl.HttpMessage;

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

    [TestMethod]
    public void Constructor_NullProtocol_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpResponseHead(null!, HttpStatus.Ok));
    }

    [TestMethod]
    public void Constructor_StatusAlone_WritesHttp11()
    {
        var head = new HttpResponseHead(HttpStatus.Ok);

        Assert.AreSame(HttpMessageProtocol.Http11, head.Protocol);
    }

    [TestMethod]
    public void ToBytes_Rtsp10_WritesAnRtspStatusLine()
    {
        var head = new HttpResponseHead(HttpMessageProtocol.Rtsp10, HttpStatus.Ok)
            .AddField("CSeq", "1");

        Assert.AreSame(HttpMessageProtocol.Rtsp10, head.Protocol);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n", Encoding.Latin1.GetString(head.ToBytes()));
    }

    [TestMethod]
    public void AddChallengeFields_Values_AddsOneWwwAuthenticateFieldPerValueInOrder()
    {
        var head = new HttpResponseHead(HttpStatus.Unauthorized)
            .AddField("Content-Length", "0")
            .AddChallengeFields(["Basic realm=\"surl\"", "Bearer"])
            .AddField("Connection", "close");

        Assert.AreEqual(
            "HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\nWWW-Authenticate: Basic realm=\"surl\"\r\nWWW-Authenticate: Bearer\r\nConnection: close\r\n\r\n",
            Encoding.Latin1.GetString(head.ToBytes()));
    }

    [TestMethod]
    public void AddChallengeFields_NoValues_AddsNothing()
    {
        var head = new HttpResponseHead(HttpStatus.Ok).AddChallengeFields([]);

        Assert.AreEqual("HTTP/1.1 200 OK\r\n\r\n", Encoding.Latin1.GetString(head.ToBytes()));
    }

    [TestMethod]
    public void AddChallengeFields_NullValues_Throws()
    {
        var head = new HttpResponseHead(HttpStatus.Ok);

        Assert.ThrowsExactly<ArgumentNullException>(() => head.AddChallengeFields(null!));
    }
}
