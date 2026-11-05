using Surl.HttpMessage;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class OriginResponseBodyTests
{
    private static OriginResponseBody Of(string method, int code, params string[] lines) =>
        OriginResponseBody.Of(method, new OriginResponseHead(1, code, "R", lines.Select(line => line.Split(": ", 2)).Select(parts => new HttpRequestField(parts[0], parts[1])).ToList()));

    [TestMethod]
    public void Of_HeadRequest_HasNoBody()
    {
        Assert.AreEqual(new OriginResponseBody(OriginResponseBodyKind.None, 0), Of("HEAD", 200, "Content-Length: 9"));
    }

    [TestMethod]
    public void Of_ContentLength_IsThatLength()
    {
        Assert.AreEqual(new OriginResponseBody(OriginResponseBodyKind.Length, 9), Of("GET", 200, "Content-Length: 9", "content-length: 9"));
    }

    [TestMethod]
    [DataRow("Transfer-Encoding: gzip, chunked", true)]
    [DataRow("Transfer-Encoding: chunked, gzip", false)]
    public void Of_TransferEncoding_IsChunkedOnlyWhenChunkedIsLast(string field, bool isChunked)
    {
        Assert.AreEqual(isChunked ? OriginResponseBodyKind.Chunked : OriginResponseBodyKind.UntilClose, Of("GET", 200, field, "Content-Length: 4").Kind);
    }

    [TestMethod]
    public void Of_NoLength_IsUntilTheOriginCloses()
    {
        Assert.AreEqual(OriginResponseBodyKind.UntilClose, Of("GET", 200).Kind);
    }

    [TestMethod]
    [DataRow("Content-Length: -1")]
    [DataRow("Content-Length: 1, 2")]
    public void Of_MalformedOrDisagreeingLengths_IsInvalid(string field)
    {
        Assert.AreEqual(OriginResponseBodyKind.Invalid, Of("GET", 200, field).Kind);
    }
}
