using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class InvalidContentLengthTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RecordedContentLengthAbc_Answers400AndCloses()
    {
        var (connection, log) = await ServeAsync(
            [RecordedFixture.ReadRequestBytes("invalid-content-length-400")], TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedResponse("invalid-content-length-400"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("GET /file.txt: the Content-Length is not one field of decimal digits; answered 400 and closed.", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("Content-Length: abc\r\n")]
    [DataRow("Content-Length: -1\r\n")]
    [DataRow("Content-Length: 3\r\nContent-Length: 4\r\n")]
    public async Task InvalidContentLength_Answers400WithConnectionClose(string fields)
    {
        var request = $"POST /file.txt HTTP/1.1\r\nHost: h\r\n{fields}\r\nabcd";

        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, peerHalfCloses: false);

        Assert.AreEqual(Latin1(RecordedResponse("invalid-content-length-400")), Latin1(connection.WrittenBytes));
        Assert.Contains("Connection: close\r\n", Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }
}
