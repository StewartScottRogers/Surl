using Surl.Content;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's ListBuckets, whose <c>ListAllMyBucketsResult</c> is written from the S3 API
/// Reference's "ListBuckets" example (ADR-0108, decisions 1 and 3; ADR-0102, decision 2.4).
/// </summary>
[TestClass]
public sealed class S3ListBucketsTests
{
    private const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<ListAllMyBucketsResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">";

    private const string Created = "<CreationDate>2026-11-05T12:00:00.000Z</CreationDate>";

    private const string ExampleOwner = "<Owner><DisplayName>AKIAIOSFODNN7EXAMPLE</DisplayName><ID>AKIAIOSFODNN7EXAMPLE</ID></Owner>";

    [TestMethod]
    public async Task ServeAsync_ListBuckets_ListsTheTopLevelFoldersButNotFilesOrSurl()
    {
        var harness = new S3ServerHarness(exposure: new ContentExposureOptions { ListDirectories = true, ServeDotFiles = true });
        harness.CreateFolder("photos");
        harness.CreateFolder("DOC-EXAMPLE-BUCKET");
        harness.CreateFolder(".surl");
        harness.WriteFile("top.txt", "not a bucket");

        var response = await harness.SendAsync("GET", "/");

        var body = Declaration
            + $"<Buckets><Bucket>{Created}<Name>DOC-EXAMPLE-BUCKET</Name></Bucket><Bucket>{Created}<Name>photos</Name></Bucket></Buckets>"
            + ExampleOwner + "</ListAllMyBucketsResult>";
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + $"Content-Type: application/xml\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ListBucketsWithNoBuckets_ListsNone()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);

        var response = await harness.SendAsync("GET", "/");

        StringAssert.EndsWith(response, $"{Declaration}<Buckets></Buckets>{ExampleOwner}</ListAllMyBucketsResult>");
    }

    [TestMethod]
    public async Task ServeAsync_ListBuckets_ListsOnlyTheSharesTheCallerMayRead()
    {
        var readable = new ContentShare("readable", S3ServerHarness.Root, new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now)));
        var hidden = new ContentShare("hidden", S3ServerHarness.Root, new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now)))
        {
            Grants = new Dictionary<string, ContentShareAccess> { ["someone-else"] = ContentShareAccess.Read },
        };
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, shares: [readable, hidden]);
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("GET", "/");

        StringAssert.Contains(response, $"<Buckets><Bucket>{Created}<Name>photos</Name></Bucket><Bucket>{Created}<Name>readable</Name></Bucket></Buckets>");
    }

    [TestMethod]
    public async Task ServeAsync_ListBuckets_ListsEachConfiguredBucketOnce()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, buckets: [new ContentBucket("media", "/photos"), new ContentBucket("photos", "/photos")]);
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("GET", "/");

        StringAssert.Contains(response, $"<Buckets><Bucket>{Created}<Name>media</Name></Bucket><Bucket>{Created}<Name>photos</Name></Bucket></Buckets>");
    }

    [TestMethod]
    public async Task ServeAsync_ListBuckets_LeavesOutConfiguredBucketsTheCallerCannotReachAsAFolder()
    {
        var hidden = new ContentShare("hidden", S3ServerHarness.Root, new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now)))
        {
            Grants = new Dictionary<string, ContentShareAccess> { ["someone-else"] = ContentShareAccess.Read },
        };
        var harness = new S3ServerHarness(
            exposure: S3ServerHarness.Writable,
            shares: [hidden],
            buckets: [new ContentBucket("gone", "/missing"), new ContentBucket("secret", "/hidden"), new ContentBucket("file", "/top.txt")]);
        harness.WriteFile("top.txt", "not a bucket");

        var response = await harness.SendAsync("GET", "/");

        StringAssert.Contains(response, "<Buckets></Buckets>");
    }

    [TestMethod]
    public async Task ServeAsync_ListBucketsWithListingsOff_Is403AccessDenied()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("GET", "/");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousListBuckets_NamesTheAnonymousOwner()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, policy: new UnitTestS3AuthenticationPolicy(UnitTestUnsignedRequests.Anonymous));

        var response = await harness.SendAsync("GET", "/");

        StringAssert.Contains(response, "<Owner><DisplayName>anonymous</DisplayName><ID>anonymous</ID></Owner>");
    }

    [TestMethod]
    public async Task ServeAsync_ListBuckets_EscapesABucketNamesXmlCharacters()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("a&b");

        var response = await harness.SendAsync("GET", "/");

        StringAssert.Contains(response, "<Name>a&amp;b</Name>");
    }
}
