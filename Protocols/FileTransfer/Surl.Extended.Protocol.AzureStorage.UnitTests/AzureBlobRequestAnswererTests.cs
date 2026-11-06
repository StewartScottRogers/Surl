using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Where the Blob answerer finds a blob's properties when the object metadata index has none.
/// </summary>
[TestClass]
public sealed class AzureBlobRequestAnswererTests
{
    [TestMethod]
    public async Task AnswerAsync_BlobInAShare_AnswersTheIndexDefaults()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var harness = new AzureServerHarness(shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendWithBearerAsync("GET", "/shared/a.txt");

        StringAssert.Contains(response, "Content-Type: text/plain\r\nContent-MD5: XrY7u+Ae7tCTyyK7j1rNww==\r\nETag: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"\r\n");
        StringAssert.EndsWith(response, "hello world");
    }

    [TestMethod]
    public async Task AnswerAsync_DeleteBlob_RemovesTheFileAndAnswers202()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt", "x-ms-delete-snapshots: include");

        Assert.AreEqual(
            "HTTP/1.1 202 Accepted\r\n" + AzureServerHarness.CommonFields(response) + "Content-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    [DataRow("/photos/missing.txt", DisplayName = "no file")]
    [DataRow("/photos/a%00b", DisplayName = "a name the store refuses")]
    public async Task AnswerAsync_DeleteOfAMissingBlob_Is404BlobNotFound(string target)
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("DELETE", target);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "404 Not Found", "BlobNotFound", "The specified blob does not exist."),
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_DeleteBlobWithUploadsOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task AnswerAsync_AnonymousListBlobsUnderAllowAnonymous_IsServed()
    {
        var harness = new AzureServerHarness(allowsAnonymous: true, exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos?restype=container&comp=list");

        StringAssert.Contains(response, "<Blob><Name>a.txt</Name>");
    }

    [TestMethod]
    public async Task AnswerAsync_AnonymousDeleteUnderAllowAnonymous_Is401NoAuthenticationInformation()
    {
        var harness = new AzureServerHarness(allowsAnonymous: true, exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("DELETE", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task AnswerAsync_IndexThatCannotBeWritten_ServesTheBlobAndNotesTheFailure()
    {
        var harness = new AzureServerHarness(maxTotalBytes: 11, persistsIndex: true);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.EndsWith(response, "hello world");
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }
}
