namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Which object requests with a query the server recognises as a multipart operation (ADR-0108,
/// decision 1's table); every other is <c>501 NotImplemented</c>.
/// </summary>
[TestClass]
public sealed class S3OperationRecognitionTests
{
    [TestMethod]
    [DataRow("GET", "/photos/a.txt?uploadId=x", DisplayName = "ListParts")]
    [DataRow("POST", "/photos/a.txt?tagging", DisplayName = "POST with another query")]
    [DataRow("POST", "/photos/a.txt?uploads&partNumber=1", DisplayName = "uploads with partNumber")]
    [DataRow("GET", "/photos/a.txt?uploads", DisplayName = "uploads by GET")]
    [DataRow("POST", "/photos/a.txt?uploadId=x&partNumber=1", DisplayName = "POST with partNumber")]
    [DataRow("PUT", "/photos/a.txt?uploadId=x", DisplayName = "PUT without partNumber")]
    [DataRow("PUT", "/photos/a.txt?partNumber=1", DisplayName = "PUT without uploadId")]
    [DataRow("DELETE", "/photos/a.txt?uploadId=x&partNumber=1", DisplayName = "DELETE with partNumber")]
    [DataRow("DELETE", "/photos/a.txt?versionId=x", DisplayName = "DELETE with another query")]
    public async Task ServeAsync_AnObjectQueryNotServed_Is501NotImplemented(string method, string target)
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendAsync(method, target);

        StringAssert.StartsWith(response, "HTTP/1.1 501 Not Implemented\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartCopy_Is501NotImplemented()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("PUT", "/photos/a.txt?partNumber=1&uploadId=x", "x-amz-copy-source: /photos/b.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 501 Not Implemented\r\n");
    }
}
