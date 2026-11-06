using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's GetObject and HeadObject, whose bytes are written from the S3 API Reference's
/// "GetObject", "HeadObject", "Error responses" and "Common Response Headers" pages (ADR-0108).
/// </summary>
[TestClass]
public sealed class S3ProtocolServerTests
{
    // MD5("hello world"), the ETag a file with no metadata entry answers.
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var harness = new S3ServerHarness();

        Assert.ThrowsExactly<ArgumentNullException>(() => new S3ProtocolServer(null!, harness.Index, harness.Policy, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new S3ProtocolServer(harness.Store, null!, harness.Policy, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new S3ProtocolServer(harness.Store, harness.Index, null!, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new S3ProtocolServer(harness.Store, harness.Index, harness.Policy, null!));
    }

    [TestMethod]
    public void Schemes_AreS3Only()
    {
        var server = new S3ServerHarness().Server;

        CollectionAssert.AreEqual(new[] { "s3" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var harness = new S3ServerHarness();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(null!, harness.Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task ServeAsync_GetObject_ReturnsTheStoredBytesWithTheObjectHeaders()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt");

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + $"Last-Modified: {S3ServerHarness.NowHttpDate}\r\nETag: {HelloWorldETag}\r\nAccept-Ranges: bytes\r\n"
                + "Content-Type: text/plain\r\nContent-Length: 11\r\nConnection: close\r\n\r\nhello world",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_GetObjectInAFolder_ReturnsTheStoredBytes()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/2026/a b.txt", "nested");

        var response = await harness.SendAsync("GET", "/photos/2026/a%20b.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nnested");
    }

    [TestMethod]
    public async Task ServeAsync_GetObjectWithStoredMetadata_SendsEveryStoredFieldAndUserMetadata()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        await harness.Index.StoreAsync(
            "photos/a.txt",
            new ObjectMetadata
            {
                ContentType = "text/plain",
                ContentEncoding = "gzip",
                ContentDisposition = "attachment",
                CacheControl = "no-cache",
                ContentLanguage = "en",
                UserMetadata = new Dictionary<string, string> { ["colour"] = "blue", ["owner"] = "ann" },
                ETag = "\"stored\"",
            },
            CancellationToken.None);

        var response = await harness.SendAsync("GET", "/photos/a.txt");

        StringAssert.Contains(
            response,
            "ETag: \"stored\"\r\nAccept-Ranges: bytes\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\n"
                + "Content-Disposition: attachment\r\nCache-Control: no-cache\r\nContent-Language: en\r\n"
                + "x-amz-meta-colour: blue\r\nx-amz-meta-owner: ann\r\nContent-Length: 11\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_HeadObject_ReturnsTheSameHeadersAndNoBody()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("HEAD", "/photos/a.txt");

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + $"Last-Modified: {S3ServerHarness.NowHttpDate}\r\nETag: {HelloWorldETag}\r\nAccept-Ranges: bytes\r\n"
                + "Content-Type: text/plain\r\nContent-Length: 11\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_MissingKey_Is404NoSuchKey()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("GET", "/photos/2026/a.jpg");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "404 Not Found",
                "<Code>NoSuchKey</Code><Message>The specified key does not exist.</Message><Key>2026/a.jpg</Key><Resource>/photos/2026/a.jpg</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    [DataRow("/photos/folder", DisplayName = "a folder")]
    [DataRow("/photos/a.txt/", DisplayName = "a file named as a folder")]
    [DataRow("/photos/a%2Fb", DisplayName = "a key the store cannot hold")]
    public async Task ServeAsync_KeyThatIsNoFile_Is404NoSuchKey(string target)
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/folder");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", target);

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.Contains(response, "<Code>NoSuchKey</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_MissingBucket_Is404NoSuchBucket()
    {
        var harness = new S3ServerHarness();

        var response = await harness.SendAsync("GET", "/photos/a.jpg");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "404 Not Found",
                "<Code>NoSuchBucket</Code><Message>The specified bucket does not exist.</Message><BucketName>photos</BucketName><Resource>/photos/a.jpg</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    [DataRow("/top.txt/a.jpg", DisplayName = "a top-level file")]
    [DataRow("/.surl/objects", DisplayName = "the service-state folder")]
    [DataRow("/..%2F/a.jpg", DisplayName = "a bucket the store refuses")]
    public async Task ServeAsync_BucketThatIsNoTopLevelFolder_Is404NoSuchBucket(string target)
    {
        var harness = new S3ServerHarness(persistsIndex: true);
        harness.WriteFile("top.txt", "a file");
        harness.CreateFolder(".surl");
        harness.CreateFolder(".surl/objects");

        var response = await harness.SendAsync("GET", target);

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.Contains(response, "<Code>NoSuchBucket</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_HeadOfAMissingKey_Is404WithNoBody()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("HEAD", "/photos/a.jpg");

        Assert.AreEqual(
            "HTTP/1.1 404 Not Found\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + "Content-Type: application/xml\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_RangeRequest_Is206WithContentRange()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt", "Range: bytes=6-");

        StringAssert.StartsWith(response, "HTTP/1.1 206 Partial Content\r\n");
        StringAssert.EndsWith(response, "Content-Type: text/plain\r\nContent-Range: bytes 6-10/11\r\nContent-Length: 5\r\nConnection: close\r\n\r\nworld");
    }

    [TestMethod]
    public async Task ServeAsync_UnsatisfiableRange_Is416InvalidRange()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt", "Range: bytes=11-20");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "416 Range Not Satisfiable",
                "<Code>InvalidRange</Code><Message>The requested range is not satisfiable</Message><Resource>/photos/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_SeveralRanges_ServesTheWholeObject()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt", "Range: bytes=0-1,4-5");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "Content-Length: 11\r\nConnection: close\r\n\r\nhello world");
    }

    [TestMethod]
    public async Task ServeAsync_HeadWithRange_Is206WithNoBody()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("HEAD", "/photos/a.txt", "Range: bytes=-5");

        StringAssert.StartsWith(response, "HTTP/1.1 206 Partial Content\r\n");
        StringAssert.EndsWith(response, "Content-Range: bytes 6-10/11\r\nContent-Length: 5\r\nConnection: close\r\n\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_FailedIfMatch_Is412PreconditionFailed()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt", "If-Match: \"other\"");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "412 Precondition Failed",
                "<Code>PreconditionFailed</Code><Message>At least one of the preconditions you specified did not hold.</Message><Resource>/photos/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_MatchingIfNoneMatch_Is304WithTheTagAndDate()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt", $"If-None-Match: {HelloWorldETag}");

        Assert.AreEqual(
            "HTTP/1.1 304 Not Modified\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + $"Last-Modified: {S3ServerHarness.NowHttpDate}\r\nETag: {HelloWorldETag}\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    [DataRow("DELETE", "/photos/a.txt?versionId=1", DisplayName = "DeleteObject of a version")]
    [DataRow("GET", "/?prefix=p", DisplayName = "ListBuckets with a query")]
    [DataRow("PUT", "/photos?versioning", DisplayName = "PutBucketVersioning")]
    [DataRow("POST", "/", DisplayName = "POST on the service")]
    [DataRow("GET", "/photos", DisplayName = "ListObjects version 1")]
    [DataRow("GET", "/photos?list-type=1", DisplayName = "ListObjects version 1 by its list-type")]
    [DataRow("PUT", "/photos/a.txt?tagging", DisplayName = "PutObjectTagging")]
    [DataRow("PUT", "/photos", DisplayName = "CreateBucket")]
    [DataRow("DELETE", "/photos/", DisplayName = "DeleteBucket")]
    [DataRow("GET", "/photos/a.txt?acl", DisplayName = "a subresource")]
    [DataRow("GET", "/photos/a.txt?partNumber=1", DisplayName = "GetObject by part")]
    [DataRow("OPTIONS", "*", DisplayName = "OPTIONS")]
    public async Task ServeAsync_UnsupportedOperation_Is501NotImplemented(string method, string target)
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync(method, target);

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "501 Not Implemented",
                $"<Code>NotImplemented</Code><Message>A header you provided implies functionality that is not implemented</Message><Resource>{target.Split('?')[0]}</Resource><RequestId>{{RequestId}}</RequestId>"),
            response);
        Assert.AreEqual("hello world", ReadFile(harness, "photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_KeyOver1024Bytes_Is400KeyTooLongError()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        var key = new string('k', 1025);

        var response = await harness.SendAsync("GET", $"/photos/{key}");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>KeyTooLongError</Code><Message>Your key is too long.</Message>");
    }

    [TestMethod]
    public async Task ServeAsync_HeadPastMaxRequestHead_IsRefused400RequestHeaderSectionTooLargeAndClosed()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 64 };

        var response = await harness.SendRawAsync($"GET /photos/a.txt HTTP/1.1\r\nHost: s3.test\r\nX-Padding: {new string('p', 100)}\r\n\r\n", limits);

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                "<Code>RequestHeaderSectionTooLarge</Code><Message>Your request header section exceeds the maximum allowed size.</Message><Resource></Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadOfAnUnsupportedVersion_KeepsTheStandard505()
    {
        var harness = new S3ServerHarness();

        var response = await harness.SendRawAsync("GET /photos/a.txt HTTP/2.0\r\nHost: s3.test\r\n\r\n");

        Assert.AreEqual(
            $"HTTP/1.1 505 HTTP Version Not Supported\r\nDate: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_KeepAlive_AnswersEachRequestWithItsOwnRequestId()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendRawAsync(
            "HEAD /photos/a.txt HTTP/1.1\r\nHost: s3.test\r\n\r\nGET /photos/b.txt HTTP/1.1\r\nHost: s3.test\r\nConnection: close\r\n\r\n");

        var second = response[response.IndexOf("HTTP/1.1 404", StringComparison.Ordinal)..];
        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreNotEqual(S3ServerHarness.RequestIdOf(response), S3ServerHarness.RequestIdOf(second));
        StringAssert.Contains(second, $"<RequestId>{S3ServerHarness.RequestIdOf(second)}</RequestId>");
    }

    [TestMethod]
    public async Task WriteRefusalAsync_PastAConnectionLimit_Writes503SlowDownAndCloses()
    {
        var harness = new S3ServerHarness();
        var connection = new InMemoryConnection([]);

        await harness.Server.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, CancellationToken.None);

        var response = Encoding.UTF8.GetString(connection.WrittenBytes);
        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "503 Service Unavailable",
                "<Code>SlowDown</Code><Message>Please reduce your request rate.</Message><Resource></Resource><RequestId>{RequestId}</RequestId>"),
            response);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        var server = new S3ServerHarness().Server;

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnectionsFromAddress, CancellationToken.None).AsTask());
    }

    private static string ReadFile(S3ServerHarness harness, string relativePath)
    {
        using var stream = harness.FileSystem.OpenFileForAsyncRead(Path.Join(S3ServerHarness.Root, relativePath));
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
