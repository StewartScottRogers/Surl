using System.Text;
using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake endpoint's Path - List, whose bodies are written from the Data Lake Storage Gen2
/// REST API page "Path - List" - its <c>PathList</c> example, <c>{"paths":[…]}</c>, and its
/// <c>x-ms-continuation</c> header (ADR-0109, decision 5.4).
/// </summary>
[TestClass]
public sealed class AzurePathListTests
{
    private const string Time = AzureServerHarness.NowHttpDate;

    [TestMethod]
    public async Task ServeAsync_DirectoryCreatedAndListedRecursively_PagesWithAContinuation()
    {
        var harness = Listable();
        harness.WriteFile("photos/a.txt", "hello world");
        await harness.SendWithBearerAsync("PUT", "/photos/2026?resource=directory");
        harness.WriteFile("photos/2026/b.txt", "b");

        var first = await harness.SendWithBearerAsync("GET", "/photos?resource=filesystem&recursive=true&maxResults=2");
        var continuation = AzureListMarker.Encode("2026/b.txt");
        var second = await harness.SendWithBearerAsync("GET", $"/photos?resource=filesystem&recursive=true&maxResults=2&continuation={continuation}");

        var firstBody = $"{{\"paths\":[{DirectoryEntry("2026")},{FileEntry("2026/b.txt", 1, "92eb5ffee6ae2fec3ad71c777531578f")}]}}";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(first)}Content-Type: application/json;charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(firstBody)}\r\n"
                + $"x-ms-continuation: {continuation}\r\nConnection: close\r\n\r\n{firstBody}",
            first);
        var secondBody = $"{{\"paths\":[{FileEntry("a.txt", 11, "5eb63bbbe01eeed093cb22bb8f5acdc3")}]}}";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(second)}Content-Type: application/json;charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(secondBody)}\r\n"
                + $"Connection: close\r\n\r\n{secondBody}",
            second);
    }

    [TestMethod]
    public async Task ServeAsync_ContinuationInsideADirectory_ResumesWithinIt()
    {
        var harness = Listable();
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/2026/a.txt", "a");
        harness.WriteFile("photos/2026/b.txt", "b");

        var response = await harness.SendWithBearerAsync("GET", $"/photos?resource=filesystem&recursive=true&continuation={AzureListMarker.Encode("2026/a.txt")}");

        CollectionAssert.AreEqual(new[] { "2026/b.txt" }, NamesOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_ListOneLevelOfADirectory_LeavesOutWhatItsDirectoriesHold()
    {
        var harness = Listable();
        harness.CreateFolder("photos/2026");
        harness.CreateFolder("photos/2026/11");
        harness.WriteFile("photos/2026/11/x.txt", "x");
        harness.WriteFile("photos/2026/a.txt", "a");
        harness.WriteFile("photos/b.txt", "b");

        var response = await harness.SendWithBearerAsync("GET", "/photos?resource=filesystem&recursive=false&directory=2026&upn=true");

        CollectionAssert.AreEqual(new[] { "2026/11", "2026/a.txt" }, NamesOf(response));
        Assert.IsFalse(response.Contains("x-ms-continuation", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ServeAsync_ListWithAMaxResultsPastTheCap_ServesTheCap()
    {
        var harness = Listable();
        harness.WriteFile("photos/a.txt", "a");

        var response = await harness.SendWithBearerAsync("GET", "/photos?resource=filesystem&recursive=false&maxResults=9999");

        CollectionAssert.AreEqual(new[] { "a.txt" }, NamesOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_FileDeletedWhileTheWalkRuns_IsLeftOut()
    {
        var inner = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(inner, "a.txt", "a");
        inner.CreateDirectory(Path.Join(AzureServerHarness.Root, "folder"));
        var shareFileSystem = new UnitTestDeletingContentFileSystem(inner, Path.Join(AzureServerHarness.Root, "folder"), Path.Join(AzureServerHarness.Root, "a.txt"));
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]).ServingDataLake();

        var response = await harness.SendWithBearerAsync("GET", "/shared?resource=filesystem&recursive=true");

        CollectionAssert.AreEqual(new[] { "folder" }, NamesOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_ListWithListingsOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness().ServingDataLake();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/photos?resource=filesystem&recursive=true");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ListWithoutRecursive_Is400MissingRequiredQueryParameter()
    {
        var harness = Listable();

        var response = await harness.SendWithBearerAsync("GET", "/photos?resource=filesystem");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "400 Bad Request", "MissingRequiredQueryParameter", "A query parameter that's mandatory for this request is not specified."),
            response);
    }

    [TestMethod]
    [DataRow("recursive=yes", DisplayName = "recursive not a boolean")]
    [DataRow("recursive=true&maxResults=many", DisplayName = "maxResults not a number")]
    [DataRow("recursive=true&continuation=%25%25", DisplayName = "a continuation this server did not issue")]
    public async Task ServeAsync_ListWithAValueThatDoesNotParse_Is400InvalidQueryParameterValue(string query)
    {
        var harness = Listable();

        var response = await harness.SendWithBearerAsync("GET", $"/photos?resource=filesystem&{query}");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "{\"error\":{\"code\":\"InvalidQueryParameterValue\",");
    }

    [TestMethod]
    public async Task ServeAsync_ListWithMaxResultsBelowOne_Is400OutOfRangeQueryParameterValue()
    {
        var harness = Listable();

        var response = await harness.SendWithBearerAsync("GET", "/photos?resource=filesystem&recursive=true&maxResults=0");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "400 Bad Request", "OutOfRangeQueryParameterValue", "A query parameter specified in the request URI is outside the permissible range."),
            response);
    }

    [TestMethod]
    [DataRow("missing", DisplayName = "no directory")]
    [DataRow("a.txt", DisplayName = "a file")]
    [DataRow("a%00b", DisplayName = "a name the store refuses")]
    public async Task ServeAsync_ListOfADirectoryThatIsNone_Is404PathNotFound(string directory)
    {
        var harness = Listable();
        harness.WriteFile("photos/a.txt", "a");

        var response = await harness.SendWithBearerAsync("GET", $"/photos?resource=filesystem&recursive=true&directory={directory}");

        Assert.AreEqual(AzureServerHarness.ExpectedJsonError(response, "404 Not Found", "PathNotFound", "The specified path does not exist."), response);
    }

    private static string DirectoryEntry(string name) =>
        $"{{\"name\":\"{name}\",\"isDirectory\":true,\"contentLength\":0,\"lastModified\":\"{Time}\",\"etag\":\"0x{AzureServerHarness.Now.UtcTicks:X}\","
            + "\"owner\":\"$superuser\",\"group\":\"$superuser\",\"permissions\":\"rwxr-x---\"}";

    private static string FileEntry(string name, int length, string md5) =>
        $"{{\"name\":\"{name}\",\"contentLength\":{length},\"lastModified\":\"{Time}\",\"etag\":\"{md5}\","
            + "\"owner\":\"$superuser\",\"group\":\"$superuser\",\"permissions\":\"rw-r-----\"}";

    private static string[] NamesOf(string response) =>
        [.. System.Text.RegularExpressions.Regex.Matches(response, "\"name\":\"([^\"]*)\"").Select(match => match.Groups[1].Value)];

    private static AzureServerHarness Listable()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");

        return harness;
    }
}
