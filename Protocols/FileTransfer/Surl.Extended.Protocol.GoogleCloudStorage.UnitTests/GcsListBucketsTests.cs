using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// List Buckets, from the XML API page "GET Service" (ADR-0110, decision 6).
/// </summary>
[TestClass]
public sealed class GcsListBucketsTests
{
    [TestMethod]
    public async Task ServeAsync_ListBuckets_ListsTheReadableTopLevelFoldersInNameOrder()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var hidden = new ContentShare("hidden", GcsServerHarness.Root, shareFileSystem)
        {
            Grants = new Dictionary<string, ContentShareAccess> { ["bob"] = ContentShareAccess.Read },
        };
        var harness = new GcsServerHarness(
            exposure: GcsServerHarness.Writable,
            accountName: "alice",
            shares: [hidden],
            buckets: [new ContentBucket("named", "/photos"), new ContentBucket("gone", "/missing"), new ContentBucket("filed", "/top.txt"), new ContentBucket("secret", "/hidden")]);
        harness.CreateFolder("photos");
        harness.CreateFolder("Zeta");
        harness.WriteFile("top.txt", "x");

        var response = await harness.SendWithBearerAsync("GET", "/");

        StringAssert.EndsWith(
            response,
            "<?xml version='1.0' encoding='UTF-8'?><ListAllMyBucketsResult xmlns=\"http://doc.s3.amazonaws.com/2006-03-01\"><Owner><ID>alice</ID></Owner><Buckets>"
                + $"<Bucket><Name>Zeta</Name><CreationDate>{GcsServerHarness.NowListTime}</CreationDate></Bucket>"
                + $"<Bucket><Name>named</Name><CreationDate>{GcsServerHarness.NowListTime}</CreationDate></Bucket>"
                + $"<Bucket><Name>photos</Name><CreationDate>{GcsServerHarness.NowListTime}</CreationDate></Bucket>"
                + "</Buckets></ListAllMyBucketsResult>");
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousListBuckets_NamesTheAnonymousOwner()
    {
        var harness = new GcsServerHarness(allowsAnonymous: true, exposure: GcsServerHarness.Writable);

        var response = await harness.SendAsync("GET", "/");

        StringAssert.Contains(response, "<Owner><ID>anonymous</ID></Owner><Buckets></Buckets>");
    }

    [TestMethod]
    public async Task ServeAsync_ListBucketsWithoutListDirectories_Is403AccessDenied()
    {
        var harness = new GcsServerHarness();

        var response = await harness.SendWithBearerAsync("GET", "/");

        Assert.AreEqual(GcsServerHarness.ExpectedError("403 Forbidden", "AccessDenied", "Access denied."), response);
    }

    [TestMethod]
    public async Task ServeAsync_FolderOrBucketNameResolvingOutsideTheRoot_IsNoBucket()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("escape");
        harness.CreateFolder("photos");
        var fileSystem = new UnitTestEscapingContentFileSystem(harness.FileSystem, Path.Join(GcsServerHarness.Root, "escape"));
        var store = new ContentStore(GcsServerHarness.Root, fileSystem, GcsServerHarness.Writable, [], [new ContentBucket("out", "/escape")]);
        var server = new GoogleCloudStorageProtocolServer(store, harness.Index, harness.Policy, harness.TimeProvider);
        var connection = new Surl.Protocol.Abstractions.InMemoryConnection(
            [System.Text.Encoding.Latin1.GetBytes($"GET / HTTP/1.1\r\nHost: x\r\n{GcsServerHarness.BearerField}\r\nConnection: close\r\n\r\n")]);

        await server.ServeAsync(connection, harness.Context());

        StringAssert.EndsWith(
            System.Text.Encoding.UTF8.GetString(connection.WrittenBytes),
            $"<Buckets><Bucket><Name>photos</Name><CreationDate>{GcsServerHarness.NowListTime}</CreationDate></Bucket></Buckets></ListAllMyBucketsResult>");
    }
}
