using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake endpoint's Path - Create, whose bytes are written from the Data Lake Storage Gen2
/// REST API page "Path - Create" and its <c>DataLakeStorageError</c> (ADR-0109, decisions 2 and 5.5).
/// </summary>
[TestClass]
public sealed class AzurePathCreateTests
{
    private const string EmptyETag = "\"d41d8cd98f00b204e9800998ecf8427e\"";

    [TestMethod]
    public async Task ServeAsync_CreateDirectory_CreatesItAndEveryParent()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PUT", "/photos/2026/11?resource=directory");

        Assert.AreEqual(
            $"HTTP/1.1 201 Created\r\n{AzureServerHarness.CommonFields(response)}ETag: {DirectoryETag()}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual(ContentEntryKind.Directory, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "2026", "11")));
    }

    [TestMethod]
    public async Task ServeAsync_CreateDirectoryThatExists_Is201()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/2026/?resource=directory");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_CreateFileInNewDirectories_CreatesThemAndAnEmptyFile()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PUT", "/photos/2026/a.txt?resource=file");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        StringAssert.Contains(response, $"ETag: {EmptyETag}\r\n");
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/2026/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CreateFileOverAFile_EmptiesIt()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a.txt?resource=file");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CreateFileWithHeaderFields_KeepsThemForPathRead()
    {
        var harness = Writable();

        await harness.SendWithBearerAsync(
            "PUT",
            "/photos/a.txt?resource=file",
            "x-ms-content-type: text/plain",
            "x-ms-content-encoding: gzip",
            "x-ms-content-language: en",
            "x-ms-content-disposition: inline",
            "x-ms-cache-control: no-cache");
        var read = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        StringAssert.Contains(read, "Content-Type: text/plain\r\n");
        StringAssert.Contains(read, "x-ms-resource-type: file\r\nContent-Encoding: gzip\r\nContent-Language: en\r\nContent-Disposition: inline\r\nCache-Control: no-cache\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_CreateFileInAShare_AnswersTheEmptyFilesETag()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]).ServingDataLake();

        var response = await harness.SendWithBearerAsync("PUT", "/shared/a.txt?resource=file");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        StringAssert.Contains(response, $"ETag: {EmptyETag}\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_CreateFileInAReadOnlyShare_Is403AuthorizationPermissionMismatch()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        var share = new ContentShare("shared", AzureServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [share]).ServingDataLake();

        var response = await harness.SendWithBearerAsync("PUT", "/shared/a.txt?resource=file");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_CreateDirectoryInAReadOnlyShare_Is403AuthorizationPermissionMismatch()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        var share = new ContentShare("shared", AzureServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [share]).ServingDataLake();

        var response = await harness.SendWithBearerAsync("PUT", "/shared/2026?resource=directory");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_CreateWithUploadsOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness().ServingDataLake();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a.txt?resource=file");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_CreateOfANameTheStoreRefuses_Is400InvalidResourceName()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a%00b?resource=file");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "400 Bad Request", "InvalidResourceName", "The specified resource name contains invalid characters."),
            response);
    }

    [TestMethod]
    [DataRow("/photos/a.txt/b.txt?resource=file", DisplayName = "a file in the way of a parent")]
    [DataRow("/photos/a.txt?resource=directory", DisplayName = "a directory over a file")]
    [DataRow("/photos/2026?resource=file", DisplayName = "a file over a directory")]
    public async Task ServeAsync_CreateConflictingWithAnEntry_Is409PathConflict(string target)
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello");
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("PUT", target);

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "409 Conflict", "PathConflict", "The specified path, or an element of the path, exists and its resource type is invalid for this operation."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_CreateFileWithTheIndexFull_Is503ServerBusyAndRemovesTheFile()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, maxIndexEntries: 0).ServingDataLake();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a.txt?resource=file");

        StringAssert.StartsWith(response, "HTTP/1.1 503 Service Unavailable\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_IndexThatCannotBeWritten_IsNotedAndTheFileStands()
    {
        var harness = new AzureServerHarness(maxTotalBytes: 0, persistsIndex: true, exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a.txt?resource=file");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void DirectoryETag_IsTheQuotedHexadecimalTicks()
    {
        Assert.AreEqual("\"0x1F\"", AzurePathCreate.DirectoryETag(new DateTimeOffset(31, TimeSpan.Zero)));
    }

    private static string DirectoryETag() => $"\"0x{AzureServerHarness.Now.UtcTicks:X}\"";

    private static AzureServerHarness Writable()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");

        return harness;
    }
}
