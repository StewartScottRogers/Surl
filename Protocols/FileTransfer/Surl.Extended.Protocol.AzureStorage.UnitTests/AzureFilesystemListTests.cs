using System.Text;
using System.Text.RegularExpressions;
using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake endpoint's Filesystem - List and Filesystem - Get Properties, whose bodies and
/// fields are written from the Data Lake Storage Gen2 REST API pages "Filesystem - List" (its
/// <c>FilesystemList</c>, <c>{"filesystems":[…]}</c>, and <c>x-ms-continuation</c>) and "Filesystem -
/// Get Properties" (ADR-0109, decision 1).
/// </summary>
[TestClass]
public sealed partial class AzureFilesystemListTests
{
    private static readonly string NowETag = AzurePathCreate.DirectoryETag(AzureServerHarness.Now);

    [TestMethod]
    public async Task ServeAsync_ListFilesystems_PagesWithAContinuation()
    {
        var harness = Listable();
        harness.CreateFolder("logs");
        harness.CreateFolder("photos");
        harness.CreateFolder("videos");
        harness.WriteFile("top.txt", "not a filesystem");

        var first = await harness.SendWithBearerAsync("GET", "/?resource=account&maxResults=2");
        var continuation = AzureListMarker.Encode("photos");
        var second = await harness.SendWithBearerAsync("GET", $"/?resource=account&maxResults=2&continuation={continuation}");

        var firstBody = $"{{\"filesystems\":[{Entry("logs")},{Entry("photos")}]}}";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(first)}Content-Type: application/json;charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(firstBody)}\r\n"
                + $"x-ms-continuation: {continuation}\r\nConnection: close\r\n\r\n{firstBody}",
            first);
        var secondBody = $"{{\"filesystems\":[{Entry("videos")}]}}";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(second)}Content-Type: application/json;charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(secondBody)}\r\n"
                + $"Connection: close\r\n\r\n{secondBody}",
            second);
    }

    [TestMethod]
    public async Task ServeAsync_ListFilesystemsWithAPrefixAndAMaxResultsPastTheCap_ListsTheNamesThatMatch()
    {
        var harness = Listable();
        harness.CreateFolder("photos-2025");
        harness.CreateFolder("photos-2026");
        harness.CreateFolder("videos");

        var response = await harness.SendWithBearerAsync("GET", "/?resource=account&prefix=photos-&maxResults=9999");

        CollectionAssert.AreEqual(new[] { "photos-2025", "photos-2026" }, NamesOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_ListFilesystems_ListsOnlyTheFilesystemsTheCallerMayRead()
    {
        var hidden = new ContentShare("hidden", AzureServerHarness.Root, new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now)))
        {
            Grants = new Dictionary<string, ContentShareAccess>(),
        };
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [hidden]).ServingDataLake();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/?resource=account");

        CollectionAssert.AreEqual(new[] { "photos" }, NamesOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_ListFilesystemsWithListingsOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness().ServingDataLake();

        var response = await harness.SendWithBearerAsync("GET", "/?resource=account");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
    }

    [TestMethod]
    [DataRow("maxResults=many", "InvalidQueryParameterValue", DisplayName = "maxResults not a number")]
    [DataRow("maxResults=0", "OutOfRangeQueryParameterValue", DisplayName = "maxResults below 1")]
    [DataRow("continuation=%25%25", "InvalidQueryParameterValue", DisplayName = "a continuation this server did not issue")]
    public async Task ServeAsync_ListFilesystemsWithABadParameter_Is400(string query, string code)
    {
        var harness = Listable();

        var response = await harness.SendWithBearerAsync("GET", $"/?resource=account&{query}");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, $"\"code\":\"{code}\"");
    }

    [TestMethod]
    public async Task ServeAsync_AccountRequestNotServed_Is400()
    {
        var harness = Listable();

        var response = await harness.SendWithBearerAsync("HEAD", "/?resource=account");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_GetFilesystemProperties_AnswersTheFolderWithoutABody()
    {
        var harness = Listable();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("HEAD", "/photos?resource=filesystem");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(response)}Content-Length: 0\r\nETag: {NowETag}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\n"
                + "x-ms-namespace-enabled: true\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_GetPropertiesOfAMissingFilesystem_Is404FilesystemNotFoundWithoutABody()
    {
        var harness = Listable();

        var response = await harness.SendWithBearerAsync("HEAD", "/photos?resource=filesystem");

        Assert.AreEqual(
            $"HTTP/1.1 404 Not Found\r\n{AzureServerHarness.CommonFields(response)}x-ms-error-code: FilesystemNotFound\r\nConnection: close\r\n\r\n",
            response);
    }

    private static AzureServerHarness Listable() => new AzureServerHarness(exposure: AzureServerHarness.Writable).ServingDataLake();

    private static string Entry(string name) =>
        $"{{\"name\":\"{name}\",\"lastModified\":\"{AzureServerHarness.NowHttpDate}\",\"etag\":\"{NowETag.Trim('"')}\"}}";

    private static string[] NamesOf(string response) =>
        [.. FilesystemName().Matches(response).Select(match => match.Groups[1].Value)];

    [GeneratedRegex("\\{\"name\":\"([^\"]*)\"")]
    private static partial Regex FilesystemName();
}
