using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The Cloud Storage server's GET Object and HEAD Object, whose bytes are written from the XML API
/// pages "GET Object", "HEAD Object", "HTTP headers and common request headers" and "HTTP status
/// and error codes" (ADR-0110).
/// </summary>
[TestClass]
public sealed class GoogleCloudStorageProtocolServerTests
{
    // MD5("hello world") as the ETag and in base64, and CRC-32C("hello world") = 0xC99465AA.
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";
    private const string HelloWorldHash = "crc32c=yZRlqg==,md5=XrY7u+Ae7tCTyyK7j1rNww==";

    private const string ObjectFields =
        $"Last-Modified: {GcsServerHarness.NowHttpDate}\r\nETag: {HelloWorldETag}\r\nAccept-Ranges: bytes\r\nContent-Type: text/plain\r\n"
        + $"x-goog-generation: {GcsServerHarness.NowGeneration}\r\nx-goog-metageneration: 1\r\nx-goog-stored-content-encoding: identity\r\n"
        + $"x-goog-stored-content-length: 11\r\nx-goog-storage-class: STANDARD\r\nx-goog-hash: {HelloWorldHash}\r\n";

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var harness = new GcsServerHarness();

        Assert.ThrowsExactly<ArgumentNullException>(() => new GoogleCloudStorageProtocolServer(null!, harness.Index, harness.Policy, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GoogleCloudStorageProtocolServer(harness.Store, null!, harness.Policy, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GoogleCloudStorageProtocolServer(harness.Store, harness.Index, null!, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GoogleCloudStorageProtocolServer(harness.Store, harness.Index, harness.Policy, null!));
    }

    [TestMethod]
    public void Schemes_IsGs()
    {
        var server = new GcsServerHarness().Server;

        CollectionAssert.AreEqual(new[] { "gs" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var harness = new GcsServerHarness();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(null!, harness.Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task WriteRefusalAsync_ConnectionPastALimit_Is503SlowDownThenClose()
    {
        var harness = new GcsServerHarness();
        var connection = new InMemoryConnection([]);

        await harness.Server.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, CancellationToken.None);

        Assert.AreEqual(
            GcsServerHarness.ExpectedError("503 Service Unavailable", "SlowDown", "Please reduce your request rate."),
            Encoding.UTF8.GetString(connection.WrittenBytes));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ServeAsync_GetObject_ReturnsTheBytesWithXGoogHashComputedAsTheApiSays()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}{ObjectFields}Content-Length: 11\r\nConnection: close\r\n\r\nhello world",
            response);
        Assert.IsTrue(harness.Log.Entries.Any(entry => entry.Text == "Login accepted: Bearer bearer token"));
    }

    [TestMethod]
    public async Task ServeAsync_HeadObject_ReturnsTheHeadersOnly()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        Assert.AreEqual($"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}{ObjectFields}Content-Length: 11\r\nConnection: close\r\n\r\n", response);
    }

    [TestMethod]
    public async Task ServeAsync_FileWrittenWithNoMetadata_IsReadWithTheIndexDefaults()
    {
        // As FTP writes it: straight into the content store, with no object metadata.
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/2026/a b.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/2026/a%20b.txt");

        StringAssert.Contains(response, ObjectFields);
        StringAssert.EndsWith(response, "\r\n\r\nhello world");
    }

    [TestMethod]
    public async Task ServeAsync_FileInAShare_IsReadWithComputedDefaults()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        GcsServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var harness = new GcsServerHarness(shares: [new ContentShare("shared", GcsServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendWithBearerAsync("GET", "/shared/a.txt");

        Assert.AreEqual($"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}{ObjectFields}Content-Length: 11\r\nConnection: close\r\n\r\nhello world", response);
    }

    [TestMethod]
    public async Task ServeAsync_GetObjectWithStoredMetadata_SendsEveryStoredFieldAndMetadata()
    {
        var harness = new GcsServerHarness();
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
                UserMetadata = new Dictionary<string, string> { ["colour"] = "blue" },
                Generation = 42,
            },
            CancellationToken.None);

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.Contains(
            response,
            "Content-Type: text/plain\r\nContent-Encoding: gzip\r\nContent-Disposition: attachment\r\nCache-Control: no-cache\r\nContent-Language: en\r\n"
                + "x-goog-meta-colour: blue\r\nx-goog-generation: 42\r\nx-goog-metageneration: 1\r\nx-goog-stored-content-encoding: gzip\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_OneRange_Is206WithTheWholeObjectsHash()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "Range: bytes=0-4");

        Assert.AreEqual(
            $"HTTP/1.1 206 Partial Content\r\n{GcsServerHarness.CommonFields}{ObjectFields}Content-Range: bytes 0-4/11\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_UnsatisfiableRange_Is416InvalidRange()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "Range: bytes=99-");

        Assert.AreEqual(GcsServerHarness.ExpectedError("416 Range Not Satisfiable", "InvalidRange", "The requested range cannot be satisfied."), response);
    }

    [TestMethod]
    public async Task ServeAsync_FailingIfMatch_Is412PreconditionFailed()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "If-Match: \"other\"");

        Assert.AreEqual(GcsServerHarness.ExpectedError("412 Precondition Failed", "PreconditionFailed", "At least one of the pre-conditions you specified did not hold."), response);
    }

    [TestMethod]
    public async Task ServeAsync_MatchingIfNoneMatch_Is304()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", $"If-None-Match: {HelloWorldETag}");

        Assert.AreEqual(
            $"HTTP/1.1 304 Not Modified\r\n{GcsServerHarness.CommonFields}Last-Modified: {GcsServerHarness.NowHttpDate}\r\nETag: {HelloWorldETag}\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    [DataRow($"x-goog-if-generation-match: {GcsServerHarness.NowGeneration}", "200 OK", DisplayName = "the current generation")]
    [DataRow("x-goog-if-generation-match: 7", "412 Precondition Failed", DisplayName = "another generation")]
    [DataRow("x-goog-if-metageneration-match: 1", "200 OK", DisplayName = "metageneration 1")]
    [DataRow("x-goog-if-metageneration-match: 2", "412 Precondition Failed", DisplayName = "metageneration 2")]
    [DataRow("x-goog-if-generation-match: soon", "400 Bad Request", DisplayName = "not a number")]
    [DataRow("x-goog-if-metageneration-match: -1", "400 Bad Request", DisplayName = "a negative metageneration")]
    public async Task ServeAsync_GenerationCondition_DecidesAsTheHeadersPageSays(string field, string status)
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", field);

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
    }

    [TestMethod]
    [DataRow($"?generation={GcsServerHarness.NowGeneration}", "HTTP/1.1 200 OK", DisplayName = "the current generation")]
    [DataRow("?generation=7", "HTTP/1.1 404 Not Found", DisplayName = "a generation that is gone")]
    [DataRow("?generation=x", "HTTP/1.1 400 Bad Request", DisplayName = "not a number")]
    public async Task ServeAsync_GenerationQuery_ServesOnlyTheCurrentGeneration(string query, string statusLine)
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt" + query);

        StringAssert.StartsWith(response, statusLine);
    }

    [TestMethod]
    public async Task ServeAsync_MissingObject_Is404NoSuchKey()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.jpg");

        Assert.AreEqual(GcsServerHarness.ExpectedError("404 Not Found", "NoSuchKey", "The specified key does not exist."), response);
    }

    [TestMethod]
    [DataRow("/photos/2026", DisplayName = "a folder")]
    [DataRow("/photos/%2e%2e/x", DisplayName = "a name the store cannot hold")]
    public async Task ServeAsync_NameThatIsNoFile_Is404NoSuchKey(string target)
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("GET", target);

        StringAssert.Contains(response, "<Code>NoSuchKey</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_HeadOfAMissingObject_HasTheStatusAndNoBody()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/a.jpg");

        Assert.AreEqual($"HTTP/1.1 404 Not Found\r\n{GcsServerHarness.CommonFields}Content-Type: application/xml; charset=UTF-8\r\nConnection: close\r\n\r\n", response);
    }

    [TestMethod]
    [DataRow("/missing/a.jpg", DisplayName = "no such folder")]
    [DataRow("/top.txt/a.jpg", DisplayName = "a top-level file")]
    public async Task ServeAsync_MissingBucket_Is404NoSuchBucket(string target)
    {
        var harness = new GcsServerHarness();
        harness.WriteFile("top.txt", "x");

        var response = await harness.SendWithBearerAsync("GET", target);

        Assert.AreEqual(GcsServerHarness.ExpectedError("404 Not Found", "NoSuchBucket", "The specified bucket does not exist."), response);
    }

    [TestMethod]
    public async Task ServeAsync_NameOver1024Bytes_Is400KeyTooLong()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/photos/" + new string('a', 1025));

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "KeyTooLong", "Your key is too long."), response);
    }

    [TestMethod]
    public async Task ServeAsync_VirtualHostedRequest_ReadsTheHostsBucket()
    {
        var harness = new GcsServerHarness(listenHost: "gcs.test");
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendRawAsync($"GET /a.txt HTTP/1.1\r\nHost: photos.gcs.test:4443\r\n{GcsServerHarness.BearerField}\r\nConnection: close\r\n\r\n");

        StringAssert.EndsWith(response, "\r\n\r\nhello world");
    }

    [TestMethod]
    public async Task ServeAsync_ObjectInAShareTheCallerMayNotRead_Is404NoSuchBucket()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        GcsServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var share = new ContentShare("shared", GcsServerHarness.Root, shareFileSystem)
        {
            Grants = new Dictionary<string, ContentShareAccess> { ["bob"] = ContentShareAccess.Read },
        };
        var harness = new GcsServerHarness(shares: [share], accountName: "alice");

        var response = await harness.SendWithBearerAsync("GET", "/shared/a.txt");

        StringAssert.Contains(response, "<Code>NoSuchBucket</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ObjectUnderABucketName_IsReadFromItsContentPath()
    {
        var harness = new GcsServerHarness(buckets: [new ContentBucket("named", "/photos")]);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/named/a.txt");

        StringAssert.EndsWith(response, "\r\n\r\nhello world");
    }

    [TestMethod]
    public async Task ServeAsync_BucketTheStoreCannotHold_Is404NoSuchBucket()
    {
        var harness = new GcsServerHarness();

        var response = await harness.SendWithBearerAsync("GET", "/%00/a.txt");

        StringAssert.Contains(response, "<Code>NoSuchBucket</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_IndexFileThatCannotBeWritten_ServesTheObjectAndNotesTheFailure()
    {
        var harness = new GcsServerHarness(maxTotalBytes: 11, persistsIndex: true);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.EndsWith(response, "\r\n\r\nhello world");
        Assert.IsTrue(harness.Log.Entries.Any(entry => entry.Text.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_HeadPastMaxRequestHead_IsRefused400MaxMessageLengthExceededAndClosed()
    {
        var harness = new GcsServerHarness();
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 64 };
        const string body = "<?xml version='1.0' encoding='UTF-8'?><Error><Code>MaxMessageLengthExceeded</Code><Message>Your request was too big.</Message></Error>";

        var response = await harness.SendRawAsync($"GET /photos/a.txt HTTP/1.1\r\nHost: x\r\nX-Padding: {new string('p', 100)}\r\n\r\n", limits);

        Assert.AreEqual(
            $"HTTP/1.1 400 Bad Request\r\n{GcsServerHarness.CommonFields}Content-Type: application/xml; charset=UTF-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadOfAnUnsupportedVersion_KeepsTheStandard505()
    {
        var harness = new GcsServerHarness();

        var response = await harness.SendRawAsync("GET /photos/a.txt HTTP/2.5\r\nHost: x\r\n\r\n", ExchangeLimits.Default);

        StringAssert.StartsWith(response, "HTTP/1.1 505 ");
        StringAssert.Contains(response, "Content-Length: 0\r\nConnection: close\r\n\r\n");
    }
}
