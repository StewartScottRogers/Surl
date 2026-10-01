namespace Surl.HttpMessage;

[TestClass]
public sealed class HttpRequestBodyFramingTests
{
    [TestMethod]
    [DataRow("", "None", 0L)]
    [DataRow("Content-Length: 0", "None", 0L)]
    [DataRow("Content-Length: 42", "ContentLength", 42L)]
    [DataRow("Content-Length: 9223372036854775807", "ContentLength", long.MaxValue)]
    [DataRow("Transfer-Encoding: chunked", "Chunked", 0L)]
    [DataRow("Transfer-Encoding: CHUNKED", "Chunked", 0L)]
    [DataRow("Transfer-Encoding: gzip, chunked", "Unreadable", 0L)]
    [DataRow("Transfer-Encoding: chunked|Transfer-Encoding: chunked", "Unreadable", 0L)]
    [DataRow("Transfer-Encoding: chunked|Content-Length: 3", "Unreadable", 0L)]
    [DataRow("Content-Length: 3|Content-Length: 3", "InvalidContentLength", 0L)]
    [DataRow("Content-Length: 3|Content-Length: 4", "InvalidContentLength", 0L)]
    [DataRow("Content-Length: 3, 3", "InvalidContentLength", 0L)]
    [DataRow("Content-Length: ", "InvalidContentLength", 0L)]
    [DataRow("Content-Length: abc", "InvalidContentLength", 0L)]
    [DataRow("Content-Length: -1", "InvalidContentLength", 0L)]
    [DataRow("Content-Length: +1", "InvalidContentLength", 0L)]
    [DataRow("Content-Length: 9223372036854775808", "InvalidContentLength", 0L)]
    public void Of_ReadsTheDeclaredFraming(string fields, string kind, long contentLength)
    {
        var head = new HttpRequestHead("GET", "/", new Version(1, 1), Fields(fields));

        Assert.AreEqual(new HttpRequestBodyFraming(Enum.Parse<HttpRequestBodyFramingKind>(kind), contentLength), HttpRequestBodyFraming.Of(head));
    }

    [TestMethod]
    public void Of_Http10WithTransferEncoding_IsUnreadable()
    {
        var head = new HttpRequestHead("GET", "/", new Version(1, 0), Fields("Transfer-Encoding: chunked"));

        Assert.AreEqual(HttpRequestBodyFramingKind.Unreadable, HttpRequestBodyFraming.Of(head).Kind);
    }

    private static HttpRequestField[] Fields(string fields) => fields.Length == 0
        ? []
        : fields.Split('|').Select(field => field.Split(": ")).Select(parts => new HttpRequestField(parts[0], parts[1])).ToArray();
}
