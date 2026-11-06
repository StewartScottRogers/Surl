using Surl.Content;
using Surl.Protocol.Abstractions;

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
    [DataRow("If-Match: \"0123\"", DisplayName = "If-Match failing")]
    [DataRow("If-Unmodified-Since: Wed, 04 Nov 2026 12:00:00 GMT", DisplayName = "If-Unmodified-Since failing")]
    [DataRow("If-None-Match: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"", DisplayName = "If-None-Match matching")]
    [DataRow("If-Modified-Since: Thu, 05 Nov 2026 12:00:00 GMT", DisplayName = "If-Modified-Since unmodified")]
    public async Task AnswerAsync_DeleteBlobWithAFailingCondition_Is412ConditionNotMetAndNothingChanges(string condition)
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt", condition);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "412 Precondition Failed", "ConditionNotMet", "The condition specified using HTTP conditional header(s) is not met."),
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task AnswerAsync_DeleteBlobIfNoneMatchAny_Is409BlobAlreadyExists()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt", "If-None-Match: *");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "409 Conflict", "BlobAlreadyExists", "The specified blob already exists."),
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task AnswerAsync_DeleteBlobWithAMatchingIfMatch_RemovesTheFile()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt", "If-Match: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task AnswerAsync_DeleteOfAMissingBlobWithIfMatch_Is404BlobNotFound()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/missing.txt", "If-Match: *");

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
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

    [TestMethod]
    public async Task RefuseHeadNotRead_HeadPastMaxRequestHead_Is400InvalidInputAndClosed()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 64 };

        var response = await harness.SendRawAsync(
            $"GET /photos/a.txt HTTP/1.1\r\nHost: account.blob.core.windows.net\r\nx-ms-client-request-id: c1\r\nX-Padding: {new string('p', 100)}\r\n\r\n",
            limits);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "InvalidInput", "One of the request inputs is not valid."),
            response);
    }

    [TestMethod]
    public async Task RefuseHeadNotRead_HeadOfAnUnsupportedVersion_KeepsTheStandard505()
    {
        var harness = new AzureServerHarness();

        var response = await harness.SendRawAsync("GET /photos/a.txt HTTP/2.0\r\nHost: account.blob.core.windows.net\r\n\r\n");

        Assert.AreEqual(
            $"HTTP/1.1 505 HTTP Version Not Supported\r\nDate: {AzureServerHarness.NowHttpDate}\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
    }
}
