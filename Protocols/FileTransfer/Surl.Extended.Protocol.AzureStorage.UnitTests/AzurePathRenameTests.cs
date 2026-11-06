using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake endpoint's rename, Path - Create with <c>x-ms-rename-source</c>, whose requests and
/// codes are written from the Data Lake Storage Gen2 REST API page "Path - Create" (ADR-0109,
/// decision 1).
/// </summary>
[TestClass]
public sealed class AzurePathRenameTests
{
    private const string EmptyETag = "\"d41d8cd98f00b204e9800998ecf8427e\"";

    [TestMethod]
    public async Task ServeAsync_RenameAFile_MovesItThroughTheContentStoreAndKeepsItsHeaderFields()
    {
        var harness = Writable();
        await harness.SendWithBearerAsync("PUT", "/photos/old.txt?resource=file", "x-ms-content-type: text/plain");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt?mode=legacy", "x-ms-rename-source: /photos/old.txt");
        var read = await harness.SendWithBearerAsync("HEAD", "/photos/new.txt");

        Assert.AreEqual(
            $"HTTP/1.1 201 Created\r\n{AzureServerHarness.CommonFields(response)}ETag: {EmptyETag}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        StringAssert.Contains(read, "Content-Type: text/plain\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "old.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_RenameADirectory_MovesItWithEverythingInIt()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/2026/a.txt", "a");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/archive", "x-ms-rename-source: /photos/2026");

        Assert.AreEqual(
            $"HTTP/1.1 201 Created\r\n{AzureServerHarness.CommonFields(response)}ETag: {AzurePathCreate.DirectoryETag(AzureServerHarness.Now)}\r\n"
                + $"Last-Modified: {AzureServerHarness.NowHttpDate}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual("a", harness.ReadFile("photos/archive/a.txt"));
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "2026")));
    }

    [TestMethod]
    public async Task ServeAsync_RenameWhoseSourceCarriesASignature_IgnoresTheQuery()
    {
        var harness = Writable();
        harness.WriteFile("photos/old.txt", "hello");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt", "x-ms-rename-source: /photos/old.txt?sv=2025-01-05&sig=abc");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("hello", harness.ReadFile("photos/new.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_RenameOntoAFile_ReplacesIt()
    {
        var harness = Writable();
        harness.WriteFile("photos/old.txt", "new bytes");
        harness.WriteFile("photos/new.txt", "old bytes");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt", "x-ms-rename-source: /photos/old.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("new bytes", harness.ReadFile("photos/new.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_RenameWhoseSourceIsMissing_Is404SourcePathNotFound()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt", "x-ms-rename-source: /photos/missing.txt");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "404 Not Found", "SourcePathNotFound", "The source path for a rename operation does not exist."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_RenameWhoseSourceIsANameTheStoreRefuses_Is404SourcePathNotFound()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt", "x-ms-rename-source: /a%00b/old.txt");

        StringAssert.Contains(response, "\"code\":\"SourcePathNotFound\"");
    }

    [TestMethod]
    [DataRow("/photos", DisplayName = "a filesystem alone")]
    [DataRow("photos/old.txt", DisplayName = "no leading slash")]
    public async Task ServeAsync_RenameWhoseSourceNamesNoPath_Is400InvalidSourceUri(string source)
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt", $"x-ms-rename-source: {source}");

        Assert.AreEqual(AzureServerHarness.ExpectedJsonError(response, "400 Bad Request", "InvalidSourceUri", "The source URI is invalid."), response);
    }

    [TestMethod]
    public async Task ServeAsync_RenameOfADirectoryIntoItself_Is400InvalidRenameSourcePath()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/2026/inner", "x-ms-rename-source: /photos/2026/");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(
                response, "400 Bad Request", "InvalidRenameSourcePath", "The source directory cannot be the same as the destination directory, nor can the destination be a subdirectory of the source directory."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_RenameIntoAMissingDirectory_Is404RenameDestinationParentPathNotFound()
    {
        var harness = Writable();
        harness.WriteFile("photos/old.txt", "a");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/missing/new.txt", "x-ms-rename-source: /photos/old.txt");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "404 Not Found", "RenameDestinationParentPathNotFound", "The parent directory of the destination path does not exist."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_RenameOntoADirectory_Is409PathAlreadyExists()
    {
        var harness = Writable();
        harness.WriteFile("photos/old.txt", "a");
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/2026", "x-ms-rename-source: /photos/old.txt");

        Assert.AreEqual(AzureServerHarness.ExpectedJsonError(response, "409 Conflict", "PathAlreadyExists", "The specified path already exists."), response);
    }

    [TestMethod]
    public async Task ServeAsync_RenameOntoAFileWithIfNoneMatchStar_Is409PathAlreadyExistsAndChangesNothing()
    {
        var harness = Writable();
        harness.WriteFile("photos/old.txt", "new bytes");
        harness.WriteFile("photos/new.txt", "old bytes");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt", "x-ms-rename-source: /photos/old.txt", "If-None-Match: *");

        StringAssert.Contains(response, "\"code\":\"PathAlreadyExists\"");
        Assert.AreEqual("old bytes", harness.ReadFile("photos/new.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_RenameWithIfNoneMatchStarOntoNothing_Is201()
    {
        var harness = Writable();
        harness.WriteFile("photos/old.txt", "a");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt", "x-ms-rename-source: /photos/old.txt", "If-None-Match: *");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_RenameOntoANameTheStoreCannotHold_Is400InvalidResourceName()
    {
        var harness = Writable();
        harness.WriteFile("photos/old.txt", "a");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a%00b", "x-ms-rename-source: /photos/old.txt");

        StringAssert.Contains(response, "\"code\":\"InvalidResourceName\"");
    }

    [TestMethod]
    public async Task ServeAsync_RenameWithUploadsOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness().ServingDataLake();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/old.txt", "a");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt", "x-ms-rename-source: /photos/old.txt");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_RenameInAReadOnlyShare_Is403AuthorizationPermissionMismatch()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(shareFileSystem, "old.txt", "a");
        var share = new ContentShare("shared", AzureServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [share]).ServingDataLake();

        var response = await harness.SendWithBearerAsync("PUT", "/shared/new.txt", "x-ms-rename-source: /shared/old.txt");

        StringAssert.Contains(response, "\"code\":\"AuthorizationPermissionMismatch\"");
    }

    [TestMethod]
    public async Task ServeAsync_RenameAFileInAShare_MovesItWithoutTheIndex()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(shareFileSystem, "old.txt", "hello");
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]).ServingDataLake();

        var response = await harness.SendWithBearerAsync("PUT", "/shared/new.txt", "x-ms-rename-source: /shared/old.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual(ContentEntryKind.File, shareFileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "new.txt")));
        Assert.AreEqual(0, harness.Index.Count);
    }

    [TestMethod]
    public async Task ServeAsync_RenameWhenTheIndexCannotBeWritten_NotesItAndRenames()
    {
        var harness = new AzureServerHarness(maxTotalBytes: 11, persistsIndex: true, exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/old.txt", "hello world");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/new.txt", "x-ms-rename-source: /photos/old.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    private static AzureServerHarness Writable()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");

        return harness;
    }
}
