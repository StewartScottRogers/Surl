using System.Text;
using System.Text.RegularExpressions;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Azure server's List Blobs, whose <c>EnumerationResults</c> bodies are written from the
/// "List Blobs" page's response body example (ADR-0109, decision 5.3).
/// </summary>
[TestClass]
public sealed partial class AzureListBlobsTests
{
    private const string Declaration = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";

    // The Properties the page's example gives a blob holding "hello world" with the index's defaults.
    private const string HelloWorldProperties =
        $"<Properties><Creation-Time>{AzureServerHarness.NowHttpDate}</Creation-Time><Last-Modified>{AzureServerHarness.NowHttpDate}</Last-Modified>"
        + "<Etag>5eb63bbbe01eeed093cb22bb8f5acdc3</Etag><Content-Length>11</Content-Length><Content-Type>text/plain</Content-Type>"
        + "<Content-Encoding /><Content-Language /><Content-MD5>XrY7u+Ae7tCTyyK7j1rNww==</Content-MD5><Cache-Control /><Content-Disposition />"
        + "<BlobType>BlockBlob</BlobType><AccessTier>Hot</AccessTier><AccessTierInferred>true</AccessTierInferred>"
        + "<LeaseStatus>unlocked</LeaseStatus><LeaseState>available</LeaseState><ServerEncrypted>false</ServerEncrypted></Properties>";

    // The block ID every staged block in these tests has: "AAA" in base64.
    private const string StagedBlockId = "QUFB";

    // The Properties of a blob with only staged blocks: no committed content, its times the last block's.
    private const string UncommittedProperties =
        $"<Properties><Creation-Time>{AzureServerHarness.NowHttpDate}</Creation-Time><Last-Modified>{AzureServerHarness.NowHttpDate}</Last-Modified>"
        + "<Etag /><Content-Length>0</Content-Length><Content-Type /><Content-Encoding /><Content-Language /><Content-MD5 /><Cache-Control /><Content-Disposition />"
        + "<BlobType>BlockBlob</BlobType><AccessTier>Hot</AccessTier><AccessTierInferred>true</AccessTierInferred>"
        + "<LeaseStatus>unlocked</LeaseStatus><LeaseState>available</LeaseState><ServerEncrypted>false</ServerEncrypted></Properties>";

    [TestMethod]
    public async Task ServeAsync_ListBlobs_AnswersThePagesEnumerationResults()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list");

        var body = Declaration
            + "<EnumerationResults ServiceEndpoint=\"http://account.blob.core.windows.net/\" ContainerName=\"photos\">"
            + $"<Blobs><Blob><Name>a.txt</Name>{HelloWorldProperties}</Blob></Blobs><NextMarker /></EnumerationResults>";
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n" + AzureServerHarness.CommonFields(response)
                + $"Content-Type: application/xml\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ListBlobsOverTls_NamesAnHttpsServiceEndpoint()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        var request = $"GET /photos/?restype=container&comp=list HTTP/1.1\r\nHost: account.blob.core.windows.net\r\n{string.Concat(AzureServerHarness.Bearer.Select(field => field + "\r\n"))}Connection: close\r\n\r\n";
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await harness.Server.ServeAsync(connection, harness.Context());

        StringAssert.Contains(
            Encoding.UTF8.GetString(connection.WrittenBytes),
            "<EnumerationResults ServiceEndpoint=\"https://account.blob.core.windows.net/\" ContainerName=\"photos\"><Blobs></Blobs><NextMarker /></EnumerationResults>");
    }

    [TestMethod]
    public async Task ServeAsync_FiveBlobsTwoAPage_PagesThroughWithMarkers()
    {
        var harness = FiveBlobs();

        var first = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list&maxresults=2");
        var second = await harness.SendWithBearerAsync("GET", $"/photos?restype=container&comp=list&maxresults=2&marker={NextMarkerOf(first)}");
        var third = await harness.SendWithBearerAsync("GET", $"/photos?restype=container&comp=list&maxresults=2&marker={NextMarkerOf(second)}");

        CollectionAssert.AreEqual(new[] { "a.txt", "b.txt" }, BlobNamesOf(first));
        StringAssert.Contains(first, "ContainerName=\"photos\"><MaxResults>2</MaxResults><Blobs>");
        StringAssert.Contains(first, "</Blobs><NextMarker>Yi50eHQ</NextMarker></EnumerationResults>");
        CollectionAssert.AreEqual(new[] { "c.txt", "folder/2026/x.jpg" }, BlobNamesOf(second));
        StringAssert.Contains(second, "<Marker>Yi50eHQ</Marker><MaxResults>2</MaxResults>");
        CollectionAssert.AreEqual(new[] { "folder/y.jpg" }, BlobNamesOf(third));
        StringAssert.EndsWith(third, "</Blobs><NextMarker /></EnumerationResults>");
    }

    [TestMethod]
    public async Task ServeAsync_DelimiterSlash_ReturnsBlobPrefixElements()
    {
        var harness = FiveBlobs();

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list&delimiter=%2F");

        CollectionAssert.AreEqual(new[] { "a.txt", "b.txt", "c.txt" }, BlobNamesOf(response));
        StringAssert.Contains(response, "ContainerName=\"photos\"><Delimiter>/</Delimiter><Blobs>");
        StringAssert.Contains(response, "</Blob><BlobPrefix><Name>folder/</Name></BlobPrefix></Blobs><NextMarker /></EnumerationResults>");
    }

    [TestMethod]
    public async Task ServeAsync_PrefixAndDelimiter_ListsOneLevelOfAFolder()
    {
        var harness = FiveBlobs();

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list&prefix=folder%2F&delimiter=%2F");

        StringAssert.Contains(response, "<Prefix>folder/</Prefix><Delimiter>/</Delimiter><Blobs><BlobPrefix><Name>folder/2026/</Name></BlobPrefix><Blob><Name>folder/y.jpg</Name>");
    }

    [TestMethod]
    public async Task ServeAsync_BlobPrefixesPagedOneAtATime_ResumeAfterTheBlobPrefix()
    {
        var harness = FiveBlobs();
        harness.WriteFile("photos/folder/2026/z.jpg", "z");

        var first = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list&prefix=folder%2F&delimiter=%2F&maxresults=1");
        var second = await harness.SendWithBearerAsync("GET", $"/photos?restype=container&comp=list&prefix=folder%2F&delimiter=%2F&maxresults=1&marker={NextMarkerOf(first)}");

        StringAssert.Contains(first, "<Blobs><BlobPrefix><Name>folder/2026/</Name></BlobPrefix></Blobs>");
        CollectionAssert.AreEqual(new[] { "folder/y.jpg" }, BlobNamesOf(second));
        StringAssert.EndsWith(second, "<NextMarker /></EnumerationResults>");
    }

    [TestMethod]
    public async Task ServeAsync_MaxResultsPast5000_IsServedAs5000()
    {
        var harness = FiveBlobs();

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list&maxresults=99999");

        StringAssert.Contains(response, "<MaxResults>5000</MaxResults>");
        Assert.HasCount(5, BlobNamesOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_IncludeMetadata_ListsEachBlobsMetadata()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/ftp.txt", "hello world");
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/put.txt", "hello world", "x-ms-blob-type: BlockBlob", "x-ms-meta-m1: v1", "x-ms-meta-m2: v2");

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list&include=metadata,snapshots");

        StringAssert.Contains(response, "<Name>ftp.txt</Name><Properties>");
        StringAssert.Contains(response, "</Properties><Metadata /></Blob><Blob><Name>put.txt</Name>");
        StringAssert.Contains(response, "</Properties><Metadata><m1>v1</m1><m2>v2</m2></Metadata></Blob></Blobs>");
    }

    [TestMethod]
    public async Task ServeAsync_FilePutByAnotherProtocol_IsListedWithTheIndexDefaultsAndAPutBlobWithItsOwn()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/ftp.txt", "hello world");
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/put.txt", "hello world", "x-ms-blob-type: BlockBlob", "x-ms-blob-content-type: text/plain", "x-ms-blob-cache-control: no-cache");

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list");

        StringAssert.Contains(response, $"<Blob><Name>ftp.txt</Name>{HelloWorldProperties}</Blob>");
        StringAssert.Contains(response, "<Name>put.txt</Name>");
        StringAssert.Contains(response, "<Content-Type>text/plain</Content-Type><Content-Encoding /><Content-Language /><Content-MD5>XrY7u+Ae7tCTyyK7j1rNww==</Content-MD5><Cache-Control>no-cache</Cache-Control>");
    }

    [TestMethod]
    public async Task ServeAsync_BlobPutWithBlobContentMd5_IsListedWithThatContentMd5()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/put.txt", "hello world", "x-ms-blob-type: BlockBlob", "x-ms-blob-content-md5: AAECAwQFBgcICQoLDA0ODw==");

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list");

        StringAssert.Contains(response, "<Content-Language /><Content-MD5>AAECAwQFBgcICQoLDA0ODw==</Content-MD5><Cache-Control />");
    }

    [TestMethod]
    public async Task ServeAsync_ListingAShareContainer_HashesItsFiles()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendWithBearerAsync("GET", "/shared?restype=container&comp=list");

        StringAssert.Contains(response, $"<Blob><Name>a.txt</Name>{HelloWorldProperties}</Blob>");
    }

    [TestMethod]
    public async Task ServeAsync_BlobDeletedWhileThePageIsWalked_IsLeftOut()
    {
        var inner = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(inner, "a.txt", "a");
        AzureServerHarness.WriteFile(inner, "b.txt", "b");
        inner.CreateDirectory(Path.Join(AzureServerHarness.Root, "folder"));
        AzureServerHarness.WriteFile(inner, "folder/x.jpg", "x");
        var shareFileSystem = new UnitTestDeletingContentFileSystem(inner, Path.Join(AzureServerHarness.Root, "folder"), Path.Join(AzureServerHarness.Root, "a.txt"));
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendWithBearerAsync("GET", "/shared?restype=container&comp=list&maxresults=2");

        CollectionAssert.AreEqual(new[] { "b.txt" }, BlobNamesOf(response));
        StringAssert.Contains(response, "<NextMarker>Yi50eHQ</NextMarker>");
    }

    [TestMethod]
    public async Task ServeAsync_BlobNameWithXmlCharacters_IsEscaped()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a&b<c>.txt", "x");

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list");

        StringAssert.Contains(response, "<Name>a&amp;b&lt;c&gt;.txt</Name>");
    }

    [TestMethod]
    public async Task ServeAsync_FileWhoseNameTheStoreRefuses_IsLeftOut()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "x");
        harness.WriteFile("photos/b:c", "x");

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list");

        CollectionAssert.AreEqual(new[] { "a.txt" }, BlobNamesOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_ListBlobsWithListingOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ListBlobsOfAMissingContainer_Is404ContainerNotFound()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "404 Not Found", "ContainerNotFound", "The specified container does not exist."),
            response);
    }

    [TestMethod]
    [DataRow("maxresults=0", "0", DisplayName = "zero")]
    [DataRow("maxresults=-3", "-3", DisplayName = "below zero")]
    public async Task ServeAsync_MaxResultsBelowOne_Is400OutOfRangeQueryParameterValue(string parameter, string value)
    {
        var harness = FiveBlobs();

        var response = await harness.SendWithBearerAsync("GET", $"/photos?restype=container&comp=list&{parameter}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "OutOfRangeQueryParameterValue", "A query parameter specified in the request URI is outside the permissible range.",
                $"<QueryParameterName>maxresults</QueryParameterName><QueryParameterValue>{value}</QueryParameterValue><MinimumAllowed>1</MinimumAllowed>"),
            response);
    }

    [TestMethod]
    [DataRow("maxresults=two", "maxresults", "two", "The value is not an integer.", DisplayName = "maxresults that is no integer")]
    [DataRow("include=metadata,colours", "include", "colours", "The value is not one of the values List Blobs takes.", DisplayName = "an unknown include")]
    [DataRow("marker=%21%21", "marker", "!!", "The value is not a marker this server returned.", DisplayName = "a marker that is not base64url")]
    [DataRow("marker=_w", "marker", "_w", "The value is not a marker this server returned.", DisplayName = "a marker that is not UTF-8")]
    public async Task ServeAsync_BadListParameter_Is400InvalidQueryParameterValue(string parameter, string name, string value, string reason)
    {
        var harness = FiveBlobs();

        var response = await harness.SendWithBearerAsync("GET", $"/photos?restype=container&comp=list&{parameter}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "InvalidQueryParameterValue", "An invalid value was specified for one of the query parameters in the request URI.",
                $"<QueryParameterName>{name}</QueryParameterName><QueryParameterValue>{value}</QueryParameterValue><Reason>{reason}</Reason>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_IncludeUncommittedBlobs_ListsABlobWithOnlyStagedBlocksInNameOrder()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.CreateFolder("docs");
        harness.WriteFile("photos/a.txt", "a");
        harness.WriteFile("photos/c.txt", "c");
        await PutBlockAsync(harness, "/photos/b.bin");
        await PutBlockAsync(harness, "/photos/c.txt");
        await PutBlockAsync(harness, "/photos/d.txt");
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/d.txt?comp=blocklist", $"<?xml version=\"1.0\" encoding=\"utf-8\"?><BlockList><Latest>{StagedBlockId}</Latest></BlockList>");
        await PutBlockAsync(harness, "/photos/z.bin");
        await PutBlockAsync(harness, "/docs/other.bin");

        var withUncommitted = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list&include=uncommittedblobs");
        var without = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list");

        CollectionAssert.AreEqual(new[] { "a.txt", "b.bin", "c.txt", "d.txt", "z.bin" }, BlobNamesOf(withUncommitted));
        CollectionAssert.AreEqual(new[] { "a.txt", "c.txt", "d.txt" }, BlobNamesOf(without));
        StringAssert.Contains(withUncommitted, $"<Blob><Name>b.bin</Name>{UncommittedProperties}</Blob><Blob><Name>c.txt</Name><Properties>");
    }

    [TestMethod]
    public async Task ServeAsync_IncludeUncommittedBlobsAndMetadataWithAPrefix_ListsTheStagedBlobUnderThePrefixWithEmptyMetadata()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        await PutBlockAsync(harness, "/photos/2026/b.bin");
        await PutBlockAsync(harness, "/photos/2025/a.bin");
        harness.WriteFile("photos/2026/c.txt", "c");

        var response = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list&prefix=2026/&include=uncommittedblobs,metadata");

        CollectionAssert.AreEqual(new[] { "2026/b.bin", "2026/c.txt" }, BlobNamesOf(response));
        StringAssert.Contains(response, $"<Blobs><Blob><Name>2026/b.bin</Name>{UncommittedProperties}<Metadata /></Blob><Blob><Name>2026/c.txt</Name>");
    }

    [TestMethod]
    public void NameOrder_NullNames_SortAsEmpty()
    {
        Assert.AreEqual(0, AzureNameOrder.Instance.Compare(null, string.Empty));
        Assert.IsLessThan(0, AzureNameOrder.Instance.Compare(null, "a"));
        Assert.IsGreaterThan(0, AzureNameOrder.Instance.Compare("a", null));
    }

    // Five blobs: three at the top, two in folders, and an empty folder that is no blob.
    private static AzureServerHarness FiveBlobs()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "a");
        harness.WriteFile("photos/b.txt", "b");
        harness.WriteFile("photos/c.txt", "c");
        harness.CreateFolder("photos/folder");
        harness.CreateFolder("photos/folder/2026");
        harness.WriteFile("photos/folder/2026/x.jpg", "x");
        harness.WriteFile("photos/folder/y.jpg", "y");
        harness.CreateFolder("photos/zz-empty");

        return harness;
    }

    private static Task<string> PutBlockAsync(AzureServerHarness harness, string blobPath) =>
        harness.SendWithBearerAndBodyAsync("PUT", $"{blobPath}?comp=block&blockid={Uri.EscapeDataString(StagedBlockId)}", "staged");

    private static string[] BlobNamesOf(string response) =>
        [.. BlobName().Matches(response).Select(match => match.Groups[1].Value)];

    private static string NextMarkerOf(string response) => NextMarkerElement().Match(response).Groups[1].Value;

    [GeneratedRegex("<Blob><Name>([^<]*)</Name>")]
    private static partial Regex BlobName();

    [GeneratedRegex("<NextMarker>([^<]*)</NextMarker>")]
    private static partial Regex NextMarkerElement();
}
