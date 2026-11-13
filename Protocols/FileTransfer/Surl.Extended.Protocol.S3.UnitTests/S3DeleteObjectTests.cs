using Surl.Content;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's DeleteObject (the S3 API Reference's "DeleteObject"; ADR-0108, decisions 1, 3
/// and 4.1).
/// </summary>
[TestClass]
public sealed class S3DeleteObjectTests
{
    private static S3ServerHarness HarnessWithObject(ContentExposureOptions? exposure = null, UnitTestUnsignedRequests unsignedRequests = UnitTestUnsignedRequests.LoggedIn)
    {
        var harness = new S3ServerHarness(exposure: exposure ?? S3ServerHarness.Writable, policy: new UnitTestS3AuthenticationPolicy(unsignedRequests));
        harness.CreateFolder("bucket");
        harness.WriteFile("bucket/a.txt", "hello world");

        return harness;
    }

    private static string NoContent(string response) =>
        $"HTTP/1.1 204 No Content\r\nDate: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\nConnection: close\r\n\r\n";

    private static ContentEntryKind KindAt(S3ServerHarness harness, string path) =>
        harness.Store.GetEntryKind(harness.Store.MapRequestPath(path));

    [TestMethod]
    public async Task ServeAsync_DeleteObject_RemovesTheFileAndAnswers204()
    {
        var harness = HarnessWithObject();

        var response = await harness.SendAsync("DELETE", "/bucket/a.txt");

        Assert.AreEqual(NoContent(response), response);
        Assert.AreEqual(ContentEntryKind.None, KindAt(harness, "/bucket/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfAKeyThatDoesNotExist_Is204()
    {
        var harness = HarnessWithObject();

        var response = await harness.SendAsync("DELETE", "/bucket/missing.txt");

        Assert.AreEqual(NoContent(response), response);
        Assert.AreEqual(ContentEntryKind.File, KindAt(harness, "/bucket/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectWithUploadsOff_Is403AccessDeniedAndKeepsTheFile()
    {
        var harness = HarnessWithObject(new ContentExposureOptions());

        var response = await harness.SendAsync("DELETE", "/bucket/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        StringAssert.Contains(response, "<Code>AccessDenied</Code><Message>Access Denied</Message><Resource>/bucket/a.txt</Resource>");
        Assert.AreEqual(ContentEntryKind.File, KindAt(harness, "/bucket/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousDeleteUnderAllowAnonymous_Is403AccessDeniedAndKeepsTheFile()
    {
        var harness = HarnessWithObject(unsignedRequests: UnitTestUnsignedRequests.Anonymous);

        var response = await harness.SendAsync("DELETE", "/bucket/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        Assert.AreEqual(ContentEntryKind.File, KindAt(harness, "/bucket/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectInAMissingBucket_Is404NoSuchBucket()
    {
        var harness = HarnessWithObject();

        var response = await harness.SendAsync("DELETE", "/other/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.Contains(response, "<Code>NoSuchBucket</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfAKeyOver1024Bytes_Is400KeyTooLongError()
    {
        var harness = HarnessWithObject();

        var response = await harness.SendAsync("DELETE", $"/bucket/{new string('k', 1025)}");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>KeyTooLongError</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfAnEmptyFolderKey_RemovesTheFolder()
    {
        var harness = HarnessWithObject();
        harness.CreateFolder("bucket/empty");

        var response = await harness.SendAsync("DELETE", "/bucket/empty/");

        Assert.AreEqual(NoContent(response), response);
        Assert.AreEqual(ContentEntryKind.None, KindAt(harness, "/bucket/empty"));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfANonEmptyFolderKey_Is204AndKeepsTheFolder()
    {
        var harness = HarnessWithObject();
        harness.CreateFolder("bucket/full");
        harness.WriteFile("bucket/full/b.txt", "kept");

        var response = await harness.SendAsync("DELETE", "/bucket/full/");

        Assert.AreEqual(NoContent(response), response);
        Assert.AreEqual("kept", harness.ReadFile("bucket/full/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfAFolderWithoutTheSlash_Is204AndKeepsTheFolder()
    {
        var harness = HarnessWithObject();
        harness.CreateFolder("bucket/empty");

        var response = await harness.SendAsync("DELETE", "/bucket/empty");

        Assert.AreEqual(NoContent(response), response);
        Assert.AreEqual(ContentEntryKind.Directory, KindAt(harness, "/bucket/empty"));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfAKeyTheStoreCannotHold_Is204()
    {
        var harness = HarnessWithObject();

        var response = await harness.SendAsync("DELETE", "/bucket/a%01b");

        Assert.AreEqual(NoContent(response), response);
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectInAReadOnlyShare_Is403AccessDeniedAndKeepsTheFile()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        S3ServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var harness = new S3ServerHarness(
            exposure: S3ServerHarness.Writable,
            shares: [new ContentShare("shared", S3ServerHarness.Root, shareFileSystem) { IsReadOnly = true }]);

        var response = await harness.SendAsync("DELETE", "/shared/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
        Assert.AreEqual(ContentEntryKind.File, shareFileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfANonEmptyFolderObject_ForgetsTheFolderObjectAndListsTheKeysUnderIt()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("bucket");
        await harness.SendAsync("PUT", "/bucket/x/", "Content-Length: 0");
        harness.WriteFile("bucket/x/a.txt", "kept");

        var deleted = await harness.SendAsync("DELETE", "/bucket/x/");
        var listed = await harness.SendAsync("GET", "/bucket?list-type=2");

        Assert.AreEqual(NoContent(deleted), deleted);
        StringAssert.Contains(listed, "<Key>x/a.txt</Key>");
        Assert.DoesNotContain("<Key>x/</Key>", listed);
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfAFolderObjectWithAnIndexThatCannotBeWritten_Is204AndNotesTheFailure()
    {
        var harness = new S3ServerHarness(maxTotalBytes: 0, persistsIndex: true, exposure: S3ServerHarness.Writable);
        harness.CreateFolder("bucket");
        await harness.SendAsync("PUT", "/bucket/x/", "Content-Length: 0");

        var response = await harness.SendAsync("DELETE", "/bucket/x/");

        Assert.AreEqual(NoContent(response), response);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfAFolderInAWritableShare_RemovesItWithNoIndexKey()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, shares: [new ContentShare("shared", S3ServerHarness.Root, shareFileSystem)]);
        await harness.SendAsync("PUT", "/shared/x/", "Content-Length: 0");

        var response = await harness.SendAsync("DELETE", "/shared/x/");

        Assert.AreEqual(NoContent(response), response);
        Assert.AreEqual(ContentEntryKind.None, shareFileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "x")));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteObjectOfAFolderInAReadOnlyShare_Is403AccessDenied()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        shareFileSystem.CreateDirectory(Path.Join(S3ServerHarness.Root, "x"));
        var harness = new S3ServerHarness(
            exposure: S3ServerHarness.Writable,
            shares: [new ContentShare("shared", S3ServerHarness.Root, shareFileSystem) { IsReadOnly = true }]);

        var response = await harness.SendAsync("DELETE", "/shared/x/");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        Assert.AreEqual(ContentEntryKind.Directory, shareFileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "x")));
    }
}
