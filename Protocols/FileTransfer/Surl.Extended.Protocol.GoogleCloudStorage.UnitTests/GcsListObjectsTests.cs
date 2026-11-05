namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// List Objects, both versions, from the XML API page "GET Bucket (List Objects)" (ADR-0110, decision 6).
/// </summary>
[TestClass]
public sealed class GcsListObjectsTests
{
    private const string Head = "<?xml version='1.0' encoding='UTF-8'?><ListBucketResult xmlns=\"http://doc.s3.amazonaws.com/2006-03-01\">";

    private static string Contents(string name, string size = "1", string eTag = "\"c4ca4238a0b923820dcc509a6f75849b\"", string owner = "") =>
        $"<Contents><Key>{name}</Key><Generation>{GcsServerHarness.NowGeneration}</Generation><MetaGeneration>1</MetaGeneration>"
            + $"<LastModified>{GcsServerHarness.NowListTime}</LastModified><ETag>{eTag}</ETag><Size>{size}</Size>{owner}</Contents>";

    private static GcsServerHarness HarnessWithFiles(string accountName = "")
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, accountName: accountName);
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");
        harness.CreateFolder("photos/empty");
        harness.WriteFile("photos/a.txt", "1");
        harness.WriteFile("photos/b.txt", "1");
        harness.WriteFile("photos/2026/c.txt", "1");

        return harness;
    }

    private static string BodyOf(string response) => response[(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];

    [TestMethod]
    public async Task ServeAsync_ListObjects_ListsEveryObjectInNameOrder()
    {
        var harness = HarnessWithFiles();

        var response = await harness.SendWithBearerAsync("GET", "/photos");

        StringAssert.StartsWith(response, $"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}Content-Type: application/xml; charset=UTF-8\r\n");
        Assert.AreEqual(
            $"{Head}<Name>photos</Name><Prefix></Prefix><MaxKeys>1000</MaxKeys><IsTruncated>false</IsTruncated>"
                + $"{Contents("2026/c.txt")}{Contents("a.txt")}{Contents("b.txt")}</ListBucketResult>",
            BodyOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_Version1PagedByMaxKeysAndMarker_ResumesAfterTheMarker()
    {
        var harness = HarnessWithFiles();

        var first = await harness.SendWithBearerAsync("GET", "/photos?max-keys=2");
        var second = await harness.SendWithBearerAsync("GET", "/photos?max-keys=2&marker=a.txt");

        Assert.AreEqual(
            $"{Head}<Name>photos</Name><Prefix></Prefix><MaxKeys>2</MaxKeys><IsTruncated>true</IsTruncated><NextMarker>a.txt</NextMarker>"
                + $"{Contents("2026/c.txt")}{Contents("a.txt")}</ListBucketResult>",
            BodyOf(first));
        Assert.AreEqual(
            $"{Head}<Name>photos</Name><Prefix></Prefix><Marker>a.txt</Marker><MaxKeys>2</MaxKeys><IsTruncated>false</IsTruncated>"
                + $"{Contents("b.txt")}</ListBucketResult>",
            BodyOf(second));
    }

    [TestMethod]
    public async Task ServeAsync_DelimiterSlash_RollsFoldersUpIntoCommonPrefixes()
    {
        var harness = HarnessWithFiles();

        var response = await harness.SendWithBearerAsync("GET", "/photos?delimiter=/&prefix=");

        Assert.AreEqual(
            $"{Head}<Name>photos</Name><Prefix></Prefix><MaxKeys>1000</MaxKeys><Delimiter>/</Delimiter><IsTruncated>false</IsTruncated>"
                + $"{Contents("a.txt")}{Contents("b.txt")}<CommonPrefixes><Prefix>2026/</Prefix></CommonPrefixes></ListBucketResult>",
            BodyOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_Version2PagedByContinuationToken_ResumesAfterTheLastEntry()
    {
        var harness = HarnessWithFiles(accountName: "alice");

        var first = await harness.SendWithBearerAsync("GET", "/photos?list-type=2&max-keys=1&delimiter=/&fetch-owner=true");
        var second = await harness.SendWithBearerAsync("GET", "/photos?list-type=2&max-keys=5&delimiter=/&continuation-token=MjAyNi8&start-after=zzz&fetch-owner=true");

        Assert.AreEqual(
            $"{Head}<Name>photos</Name><Prefix></Prefix><KeyCount>1</KeyCount><MaxKeys>1</MaxKeys><Delimiter>/</Delimiter><IsTruncated>true</IsTruncated>"
                + "<NextContinuationToken>MjAyNi8</NextContinuationToken><CommonPrefixes><Prefix>2026/</Prefix></CommonPrefixes></ListBucketResult>",
            BodyOf(first));
        Assert.AreEqual(
            $"{Head}<Name>photos</Name><Prefix></Prefix><ContinuationToken>MjAyNi8</ContinuationToken><StartAfter>zzz</StartAfter><KeyCount>2</KeyCount>"
                + "<MaxKeys>5</MaxKeys><Delimiter>/</Delimiter><IsTruncated>false</IsTruncated>"
                + $"{Contents("a.txt", owner: "<Owner><ID>alice</ID></Owner>")}{Contents("b.txt", owner: "<Owner><ID>alice</ID></Owner>")}</ListBucketResult>",
            BodyOf(second));
    }

    [TestMethod]
    public async Task ServeAsync_Version2StartAfterAndAnonymousOwner_StartsAfterTheName()
    {
        var harness = new GcsServerHarness(allowsAnonymous: true, exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "1");
        harness.WriteFile("photos/b.txt", "1");

        var response = await harness.SendAsync("GET", "/photos?list-type=2&start-after=a.txt&fetch-owner=true");

        StringAssert.Contains(BodyOf(response), $"<StartAfter>a.txt</StartAfter><KeyCount>1</KeyCount>");
        StringAssert.Contains(BodyOf(response), Contents("b.txt", owner: "<Owner><ID>anonymous</ID></Owner>"));
    }

    [TestMethod]
    public async Task ServeAsync_EncodingTypeUrl_PercentEncodesNames()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a b.txt", "1");

        var response = await harness.SendWithBearerAsync("GET", "/photos?encoding-type=url&prefix=a%20&marker=a&max-keys=0");
        var listed = await harness.SendWithBearerAsync("GET", "/photos?encoding-type=url&max-keys=1");

        Assert.AreEqual(
            $"{Head}<Name>photos</Name><Prefix>a%20</Prefix><Marker>a</Marker><MaxKeys>0</MaxKeys><IsTruncated>false</IsTruncated></ListBucketResult>",
            BodyOf(response));
        StringAssert.Contains(BodyOf(listed), "<Key>a%20b.txt</Key>");
    }

    [TestMethod]
    public async Task ServeAsync_MaxKeysAbove1000_IsServedAs1000()
    {
        var harness = HarnessWithFiles();

        var response = await harness.SendWithBearerAsync("GET", "/photos?max-keys=5000");

        StringAssert.Contains(response, "<MaxKeys>1000</MaxKeys>");
    }

    [TestMethod]
    [DataRow("max-keys=-1", DisplayName = "a negative max-keys")]
    [DataRow("max-keys=many", DisplayName = "a max-keys that is no number")]
    [DataRow("encoding-type=base64", DisplayName = "another encoding-type")]
    [DataRow("list-type=3", DisplayName = "another list-type")]
    [DataRow("list-type=2&marker=a", DisplayName = "marker with version 2")]
    [DataRow("continuation-token=YQ", DisplayName = "continuation-token with version 1")]
    [DataRow("start-after=a", DisplayName = "start-after with version 1")]
    [DataRow("fetch-owner=true", DisplayName = "fetch-owner with version 1")]
    [DataRow("list-type=2&continuation-token=!!", DisplayName = "a token that does not decode")]
    public async Task ServeAsync_BadListingParameter_Is400InvalidArgument(string query)
    {
        var harness = HarnessWithFiles();

        var response = await harness.SendWithBearerAsync("GET", $"/photos?{query}");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidArgument", "Invalid argument."), response);
    }

    [TestMethod]
    public async Task ServeAsync_ListObjectsWithoutListDirectories_Is403AccessDenied()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/photos?list-type=1");

        Assert.AreEqual(GcsServerHarness.ExpectedError("403 Forbidden", "AccessDenied", "Access denied."), response);
    }

    [TestMethod]
    public async Task ServeAsync_VirtualHostedListing_ListsTheHostsBucket()
    {
        var harness = HarnessWithFiles();

        var response = await harness.SendRawAsync($"GET / HTTP/1.1\r\nHost: photos.storage.googleapis.com\r\n{GcsServerHarness.BearerField}\r\nConnection: close\r\n\r\n");

        StringAssert.Contains(response, "<Name>photos</Name>");
        StringAssert.Contains(response, "<Key>a.txt</Key>");
    }

    [TestMethod]
    public async Task ServeAsync_ObjectDeletedDuringTheWalk_IsLeftOut()
    {
        var harness = HarnessWithFiles();
        await harness.Index.StoreAsync("photos/b.txt", new Surl.Content.ObjectMetadata(), CancellationToken.None);
        harness.FileSystem.DeleteFile(Path.Join(GcsServerHarness.Root, "photos", "b.txt"));

        var response = await harness.SendWithBearerAsync("GET", "/photos");

        Assert.IsFalse(response.Contains("<Key>b.txt</Key>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ServeAsync_Prefix_ListsOnlyTheNamesUnderIt()
    {
        var harness = HarnessWithFiles();

        var response = await harness.SendWithBearerAsync("GET", "/photos?prefix=2026/");

        StringAssert.Contains(BodyOf(response), $"<Prefix>2026/</Prefix><MaxKeys>1000</MaxKeys><IsTruncated>false</IsTruncated>{Contents("2026/c.txt")}</ListBucketResult>");
    }
}
