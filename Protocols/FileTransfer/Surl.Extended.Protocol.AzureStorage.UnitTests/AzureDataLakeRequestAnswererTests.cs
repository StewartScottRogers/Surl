using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake endpoint's Path - Read, Path - Delete and its refusals, whose bytes are written
/// from the Data Lake Storage Gen2 REST API pages "Path - Read", "Path - Delete" and their
/// <c>DataLakeStorageError</c> (ADR-0109, decisions 1, 2 and 5.2).
/// </summary>
[TestClass]
public sealed class AzureDataLakeRequestAnswererTests
{
    [TestMethod]
    [DataRow("abfs")]
    [DataRow("abfss")]
    public async Task ServeAsync_PathReadOnEitherDataLakeScheme_ReturnsTheFile(string scheme)
    {
        var harness = new AzureServerHarness { Scheme = scheme };
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.Contains(response, "Accept-Ranges: bytes\r\nx-ms-resource-type: file\r\nConnection: close\r\n\r\nhello world");
    }

    [TestMethod]
    [DataRow("/photos/missing.txt", DisplayName = "no file")]
    [DataRow("/photos/a%00b", DisplayName = "a name the store refuses")]
    public async Task ServeAsync_ReadOfAMissingPath_Is404PathNotFound(string target)
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync("GET", target);

        Assert.AreEqual(AzureServerHarness.ExpectedJsonError(response, "404 Not Found", "PathNotFound", "The specified path does not exist."), response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadOfAMissingPath_IsTheErrorWithoutABody()
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/missing.txt");

        Assert.AreEqual(
            $"HTTP/1.1 404 Not Found\r\n{AzureServerHarness.CommonFields(response)}x-ms-error-code: PathNotFound\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ReadOfADirectory_Is409ResourceTypeMismatch()
    {
        var harness = Readable();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("GET", "/photos/2026");

        StringAssert.Contains(response, "\"code\":\"ResourceTypeMismatch\"");
    }

    [TestMethod]
    [DataRow("/missing/a.txt", DisplayName = "no folder")]
    [DataRow("/a%00b/a.txt", DisplayName = "a name the store refuses")]
    public async Task ServeAsync_PathInAMissingFilesystem_Is404FilesystemNotFound(string target)
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync("GET", target);

        Assert.AreEqual(AzureServerHarness.ExpectedJsonError(response, "404 Not Found", "FilesystemNotFound", "The specified filesystem does not exist."), response);
    }

    [TestMethod]
    public async Task ServeAsync_PathLongerThan1024Characters_Is400InvalidResourceName()
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync("GET", $"/photos/{new string('a', 1025)}");

        StringAssert.Contains(response, "\"code\":\"InvalidResourceName\"");
    }

    [TestMethod]
    public async Task ServeAsync_UnsupportedMethod_Is405UnsupportedHttpVerb()
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync("POST", "/photos/a.txt");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "405 Method Not Allowed", "UnsupportedHttpVerb", "The resource doesn't support the specified HTTP verb."),
            response);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("Azure Data Lake server", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_FilesystemRequestNotServed_Is400InvalidQueryParameterValue()
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync("PUT", "/photos?resource=filesystem");

        StringAssert.Contains(response, "\"code\":\"InvalidQueryParameterValue\"");
    }

    [TestMethod]
    public async Task ServeAsync_PathNamingNoFile_Is400InvalidUri()
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync("GET", "/photos");

        StringAssert.Contains(response, "\"code\":\"InvalidUri\"");
    }

    [TestMethod]
    public async Task ServeAsync_AclHeader_Is400UnsupportedHeader()
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a.txt?resource=file", "x-ms-acl: user::rwx");

        StringAssert.Contains(response, "\"code\":\"UnsupportedHeader\"");
    }

    [TestMethod]
    [DataRow("PUT", "/photos/a.txt", DisplayName = "Path - Create without resource")]
    [DataRow("PATCH", "/photos/a.txt", DisplayName = "Path - Update without action")]
    public async Task ServeAsync_RequestWithoutItsOperationParameter_Is400MissingRequiredQueryParameter(string method, string target)
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync(method, target);

        StringAssert.Contains(response, "\"code\":\"MissingRequiredQueryParameter\"");
    }

    [TestMethod]
    [DataRow("PUT", "/photos/a.txt?resource=symlink", DisplayName = "an unknown resource")]
    [DataRow("PATCH", "/photos/a.txt?action=setAccessControl", DisplayName = "an action not served")]
    public async Task ServeAsync_OperationParameterNotServed_Is400InvalidQueryParameterValue(string method, string target)
    {
        var harness = Readable();

        var response = await harness.SendWithBearerAsync(method, target);

        StringAssert.Contains(response, "\"code\":\"InvalidQueryParameterValue\"");
    }

    [TestMethod]
    public async Task ServeAsync_MalformedVersion_IsTheJsonError()
    {
        var harness = Readable();

        var response = await harness.SendAsync("GET", "/photos/a.txt", "x-ms-version: soon");

        StringAssert.Contains(response, "\"code\":\"InvalidHeaderValue\"");
    }

    [TestMethod]
    public async Task ServeAsync_NoCredentials_Is401WithTheChallenge()
    {
        var harness = Readable();

        var response = await harness.SendAsync("GET", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        StringAssert.Contains(response, "WWW-Authenticate: Bearer ");
        StringAssert.Contains(response, "\"code\":\"NoAuthenticationInformation\"");
    }

    [TestMethod]
    public async Task ServeAsync_DeleteFile_RemovesItAndAnswers200()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt");

        Assert.AreEqual($"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(response)}Content-Length: 0\r\nConnection: close\r\n\r\n", response);
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteFileWithStagedAppends_DropsThem()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");
        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello");

        await harness.SendWithBearerAsync("DELETE", "/photos/a.txt");
        harness.WriteFile("photos/a.txt", "");
        var flushed = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=flush&position=5");

        StringAssert.Contains(flushed, "\"code\":\"InvalidFlushPosition\"");
    }

    [TestMethod]
    public async Task ServeAsync_DeleteEmptyDirectory_RemovesIt()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/2026");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "2026")));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteNonEmptyDirectoryWithoutRecursive_Is409DirectoryNotEmpty()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/2026/a.txt", "a");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/2026?recursive=false");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "409 Conflict", "DirectoryNotEmpty", "The recursive query parameter value must be true to delete a non-empty directory."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_DeleteDirectoryRecursively_RemovesItAndAllItHolds()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");
        harness.CreateFolder("photos/2026/11");
        harness.WriteFile("photos/2026/11/x.txt", "x");
        harness.WriteFile("photos/2026/a.txt", "a");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/2026?recursive=true");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "2026")));
    }

    [TestMethod]
    [DataRow("/photos/missing.txt", DisplayName = "no file")]
    [DataRow("/photos/a%00b", DisplayName = "a name the store refuses")]
    public async Task ServeAsync_DeleteOfAMissingPath_Is404PathNotFound(string target)
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("DELETE", target);

        StringAssert.Contains(response, "\"code\":\"PathNotFound\"");
    }

    [TestMethod]
    public async Task ServeAsync_DeleteWithUploadsOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = Readable();
        harness.WriteFile("photos/a.txt", "a");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        Assert.AreEqual(ContentEntryKind.File, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteInAReadOnlyShare_Is403AuthorizationPermissionMismatch()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(shareFileSystem, "a.txt", "a");
        var share = new ContentShare("shared", AzureServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [share]).ServingDataLake();

        var response = await harness.SendWithBearerAsync("DELETE", "/shared/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public async Task RefuseHeadNotRead_HeadPastMaxRequestHead_Is400InvalidInputInTheJsonBodyAndClosed()
    {
        var harness = Readable();
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 64 };

        var response = await harness.SendRawAsync(
            $"GET /photos/a.txt HTTP/1.1\r\nHost: account.dfs.core.windows.net\r\nX-Padding: {new string('p', 100)}\r\n\r\n",
            limits);

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "400 Bad Request", "InvalidInput", "One of the request inputs is not valid."),
            response);
    }

    [TestMethod]
    public async Task RefuseHeadNotRead_HeadOfAnUnsupportedVersion_KeepsTheStandard505()
    {
        var harness = Readable();

        var response = await harness.SendRawAsync("GET /photos/a.txt HTTP/2.0\r\nHost: account.dfs.core.windows.net\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 505 HTTP Version Not Supported\r\n");
    }

    private static AzureServerHarness Readable()
    {
        var harness = new AzureServerHarness().ServingDataLake();
        harness.CreateFolder("photos");

        return harness;
    }

    private static AzureServerHarness Writable()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");

        return harness;
    }
}
