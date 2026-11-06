using System.Text;
using System.Text.RegularExpressions;
using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Blob endpoint's List Containers and Get Container Properties, whose bodies and fields are
/// written from the "List Containers" page's response body example and the "Get Container
/// Properties" page's response headers (ADR-0109, decision 1).
/// </summary>
[TestClass]
public sealed partial class AzureListContainersTests
{
    private const string Declaration = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";

    private static readonly string NowETag = AzurePathCreate.DirectoryETag(AzureServerHarness.Now);

    [TestMethod]
    public async Task ServeAsync_ListContainers_AnswersThePagesEnumerationResults()
    {
        var harness = Listable();
        harness.CreateFolder("photos");
        harness.CreateFolder("logs");
        harness.WriteFile("top.txt", "not a container");

        var response = await harness.SendWithBearerAsync("GET", "/?comp=list");

        var body = Declaration
            + "<EnumerationResults ServiceEndpoint=\"http://account.blob.core.windows.net/\"><Containers>"
            + ContainerElement("logs") + ContainerElement("photos")
            + "</Containers><NextMarker /></EnumerationResults>";
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n" + AzureServerHarness.CommonFields(response)
                + $"Content-Type: application/xml\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ListContainersWithPrefixMaxResultsAndMarker_PagesThroughTheNamesThatMatch()
    {
        var harness = Listable();
        harness.CreateFolder("photos-2025");
        harness.CreateFolder("photos-2026");
        harness.CreateFolder("photos-2027");
        harness.CreateFolder("videos");

        var first = await harness.SendWithBearerAsync("GET", "/?comp=list&prefix=photos-&maxresults=2");
        var marker = AzureListMarker.Encode("photos-2026");
        var second = await harness.SendWithBearerAsync("GET", $"/?comp=list&prefix=photos-&maxresults=2&marker={marker}");

        StringAssert.Contains(first, $"<Prefix>photos-</Prefix><MaxResults>2</MaxResults><Containers>{ContainerElement("photos-2025")}{ContainerElement("photos-2026")}</Containers><NextMarker>{marker}</NextMarker>");
        StringAssert.Contains(second, $"<Prefix>photos-</Prefix><Marker>{marker}</Marker><MaxResults>2</MaxResults><Containers>{ContainerElement("photos-2027")}</Containers><NextMarker /></EnumerationResults>");
    }

    [TestMethod]
    public async Task ServeAsync_ListContainers_ListsOnlyTheContainersTheCallerMayRead()
    {
        var hidden = new ContentShare("hidden", AzureServerHarness.Root, new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now)))
        {
            Grants = new Dictionary<string, ContentShareAccess>(),
        };
        var open = new ContentShare("open", AzureServerHarness.Root, new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now)));
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [hidden, open], buckets: [new ContentBucket("media", "/photos")]);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/?comp=list");

        CollectionAssert.AreEqual(new[] { "media", "open", "photos" }, NamesOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_ListContainersIncludingMetadata_AddsAnEmptyMetadataToEachContainer()
    {
        var harness = Listable();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/?comp=list&include=metadata,deleted,system");

        StringAssert.Contains(response, "</Properties><Metadata /></Container>");
    }

    [TestMethod]
    public async Task ServeAsync_ListContainersWithListingsOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/?comp=list");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
    }

    [TestMethod]
    [DataRow("include=tags", "<QueryParameterName>include</QueryParameterName><QueryParameterValue>tags</QueryParameterValue><Reason>The value is not one of the values List Containers takes.</Reason>", DisplayName = "an include List Containers does not take")]
    [DataRow("marker=%25%25", "<QueryParameterName>marker</QueryParameterName><QueryParameterValue>%%</QueryParameterValue><Reason>The value is not a marker this server returned.</Reason>", DisplayName = "a marker this server did not issue")]
    public async Task ServeAsync_ListContainersWithABadParameter_Is400InvalidQueryParameterValue(string query, string elements)
    {
        var harness = Listable();

        var response = await harness.SendWithBearerAsync("GET", $"/?comp=list&{query}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "InvalidQueryParameterValue", "An invalid value was specified for one of the query parameters in the request URI.", elements),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ListContainersWithMaxResultsZero_Is400OutOfRangeQueryParameterValue()
    {
        var harness = Listable();

        var response = await harness.SendWithBearerAsync("GET", "/?comp=list&maxresults=0");

        StringAssert.Contains(response, "<Code>OutOfRangeQueryParameterValue</Code>");
    }

    [TestMethod]
    [DataRow("GET", DisplayName = "GET")]
    [DataRow("HEAD", DisplayName = "HEAD")]
    public async Task ServeAsync_GetContainerProperties_AnswersTheFolderWithoutABody(string method)
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync(method, "/photos?timeout=30&restype=container");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(response)}Content-Length: 0\r\nETag: {NowETag}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\n"
                + "x-ms-lease-status: unlocked\r\nx-ms-lease-state: available\r\nx-ms-has-immutability-policy: false\r\nx-ms-has-legal-hold: false\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadOfAMissingContainersProperties_Is404ContainerNotFoundWithoutABody()
    {
        var harness = new AzureServerHarness();

        var response = await harness.SendWithBearerAsync("HEAD", "/photos?restype=container");

        Assert.AreEqual(
            $"HTTP/1.1 404 Not Found\r\n{AzureServerHarness.CommonFields(response)}x-ms-error-code: ContainerNotFound\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    [DataRow("HEAD", "/?comp=list", DisplayName = "a HEAD of the account's listing")]
    [DataRow("GET", "/?comp=stats", DisplayName = "an account request not served")]
    [DataRow("HEAD", "/photos?restype=container&comp=list", DisplayName = "a HEAD of a container's listing")]
    public async Task ServeAsync_AccountOrContainerRequestNotServed_IsRefused(string method, string target)
    {
        var harness = Listable();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync(method, target);

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
    }

    private static AzureServerHarness Listable() => new(exposure: AzureServerHarness.Writable);

    // The page example's Container for a folder last written at the harness's clock.
    private static string ContainerElement(string name) =>
        $"<Container><Name>{name}</Name><Properties><Last-Modified>{AzureServerHarness.NowHttpDate}</Last-Modified><Etag>{NowETag.Trim('"')}</Etag>"
            + "<LeaseStatus>unlocked</LeaseStatus><LeaseState>available</LeaseState><HasImmutabilityPolicy>false</HasImmutabilityPolicy>"
            + "<HasLegalHold>false</HasLegalHold></Properties></Container>";

    private static string[] NamesOf(string response) =>
        [.. ContainerName().Matches(response).Select(match => match.Groups[1].Value)];

    [GeneratedRegex("<Container><Name>([^<]*)</Name>")]
    private static partial Regex ContainerName();
}
