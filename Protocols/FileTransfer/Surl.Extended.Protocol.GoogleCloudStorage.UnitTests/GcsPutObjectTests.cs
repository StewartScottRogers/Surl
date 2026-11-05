using System.Text;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// PUT Object, from the XML API pages "PUT Object", "HTTP headers and common request headers" and
/// "HTTP status and error codes" (ADR-0110, decision 1.2).
/// </summary>
[TestClass]
public sealed class GcsPutObjectTests
{
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";
    private const string HelloWorldMd5 = "XrY7u+Ae7tCTyyK7j1rNww==";
    private const string HelloWorldCrc32c = "yZRlqg==";

    [TestMethod]
    public async Task ServeAsync_PutUnderAllowUploads_StoresTheObjectAndAnswersItsDigests()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026/a.txt", "hello world");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}ETag: {HelloWorldETag}\r\nx-goog-generation: {GcsServerHarness.NowGeneration}\r\n"
                + $"x-goog-metageneration: 1\r\nx-goog-hash: crc32c={HelloWorldCrc32c},md5={HelloWorldMd5}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/2026/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutWithoutAllowUploads_Is403AccessDeniedAndStoresNothing()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world");

        Assert.AreEqual(GcsServerHarness.ExpectedError("403 Forbidden", "AccessDenied", "Access denied."), response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutWithMetadata_IsReturnedByALaterRead()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.TimeProvider.UtcNow = GcsServerHarness.Now.AddSeconds(1);

        await harness.SendWithBearerAndBodyAsync(
            "PUT",
            "/photos/a.txt",
            "hello world",
            "Content-Type: text/plain",
            "Content-Encoding: identity",
            "Content-Disposition: inline",
            "Cache-Control: no-cache",
            "Content-Language: en",
            "X-Goog-Meta-Colour: blue",
            "x-goog-storage-class: NEARLINE",
            "x-goog-acl: public-read",
            "x-goog-project-id: 1234");
        var response = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        StringAssert.Contains(
            response,
            "Content-Type: text/plain\r\nContent-Encoding: identity\r\nContent-Disposition: inline\r\nCache-Control: no-cache\r\nContent-Language: en\r\n"
                + "x-goog-meta-colour: blue\r\nx-goog-generation: 1793880001000000\r\nx-goog-metageneration: 1\r\nx-goog-stored-content-encoding: identity\r\n"
                + "x-goog-stored-content-length: 11\r\nx-goog-storage-class: STANDARD\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutOverAnObject_ReplacesItAndItsGeneration()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "old");
        harness.TimeProvider.UtcNow = GcsServerHarness.Now.AddSeconds(2);

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world");

        StringAssert.Contains(response, "x-goog-generation: 1793880002000000\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("Content-MD5: XrY7u+Ae7tCTyyK7j1rNww==", DisplayName = "Content-MD5")]
    [DataRow("x-goog-hash: crc32c=yZRlqg==,md5=XrY7u+Ae7tCTyyK7j1rNww==", DisplayName = "both hashes in one field")]
    [DataRow("x-goog-hash: crc32c=yZRlqg==", DisplayName = "the CRC-32C alone")]
    public async Task ServeAsync_PutWithMatchingDigests_IsStored(string field)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", field);

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("Content-MD5: AAAAAAAAAAAAAAAAAAAAAA==", DisplayName = "Content-MD5")]
    [DataRow("x-goog-hash: md5=AAAAAAAAAAAAAAAAAAAAAA==", DisplayName = "the x-goog-hash MD5")]
    [DataRow("x-goog-hash: crc32c=AAAAAA==", DisplayName = "the x-goog-hash CRC-32C")]
    public async Task ServeAsync_PutWithAMismatchedDigest_Is400BadDigestAndKeepsTheOldObject(string field)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "old");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", field);

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "BadDigest", "The Content-MD5 or x-goog-hash you specified did not match what was received."), response);
        Assert.AreEqual("old", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutWithAMalformedContentMd5_Is400InvalidDigest()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "Content-MD5: AAAA");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidDigest", "The Content-MD5 you specified is not valid."), response);
    }

    [TestMethod]
    [DataRow("crc32c=AAAA", DisplayName = "a CRC-32C of three bytes")]
    [DataRow("md5=AAAA", DisplayName = "an MD5 of three bytes")]
    [DataRow("sha1=AAAA", DisplayName = "another hash")]
    public async Task ServeAsync_PutWithAMalformedXGoogHash_Is400InvalidHash(string value)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", $"x-goog-hash: {value}");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidHash", "The x-goog-hash you specified is not valid."), response);
    }

    [TestMethod]
    public async Task ServeAsync_PutWithoutContentLength_Is411MissingContentLength()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a.txt");

        Assert.AreEqual(GcsServerHarness.ExpectedError("411 Length Required", "MissingContentLength", "You must provide the Content-Length HTTP header."), response);
    }

    [TestMethod]
    public async Task ServeAsync_PutAnnouncedPastMaxFilesize_Is400EntityTooLarge()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable with { MaxUploadBytes = 4 });
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "EntityTooLarge", "Your proposed upload exceeds the maximum allowed object size."), response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutWhoseBodyEndsEarly_Is400IncompleteBodyAndStoresNothing()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendRawAsync($"PUT /photos/a.txt HTTP/1.1\r\nHost: x\r\n{GcsServerHarness.BearerField}\r\nContent-Length: 20\r\n\r\nhello");

        StringAssert.Contains(response, "<Code>IncompleteBody</Code>");
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutWithMetadataPast8KiB_Is400MetadataTooLarge()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", $"x-goog-meta-big: {new string('x', 8190)}");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "MetadataTooLarge", "Your metadata headers exceed the maximum allowed metadata size."), response);
    }

    [TestMethod]
    public async Task ServeAsync_PutWhenTheIndexIsFull_Is403InsufficientQuotaAndRemovesTheObject()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, maxIndexEntries: 1);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/b.txt", "b");
        await harness.Index.StoreAsync("photos/b.txt", new ObjectMetadata(), CancellationToken.None);

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world");

        Assert.AreEqual(GcsServerHarness.ExpectedError("403 Forbidden", "InsufficientQuota", "The object metadata index is full."), response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutWithGenerationMatchZeroAndNoObject_IsStored()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-goog-if-generation-match: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("x-goog-if-generation-match: 0", "412 Precondition Failed", DisplayName = "0 over an existing object")]
    [DataRow("x-goog-if-generation-match: x", "400 Bad Request", DisplayName = "not a number")]
    public async Task ServeAsync_PutWithAFailingGenerationCondition_IsRefusedAndKeepsTheObject(string field, string status)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "old");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", field);

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
        Assert.AreEqual("old", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutMetagenerationMatchWithNoObject_Is412PreconditionFailed()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-goog-if-metageneration-match: 1");

        StringAssert.StartsWith(response, "HTTP/1.1 412 Precondition Failed\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ZeroBytePutOfANameEndingInSlash_CreatesTheFolder()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026/", string.Empty);

        StringAssert.Contains(response, "ETag: \"d41d8cd98f00b204e9800998ecf8427e\"\r\n");
        StringAssert.Contains(response, "x-goog-hash: crc32c=AAAAAA==,md5=1B2M2Y8AsgTpgAmY7PhCfg==\r\n");
        Assert.AreEqual(ContentEntryKind.Directory, harness.EntryKind("photos/2026"));
    }

    [TestMethod]
    public async Task ServeAsync_NonEmptyPutOfANameEndingInSlash_Is400InvalidObjectName()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026/", "x");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidObjectName", "The specified object name is not valid."), response);
    }

    [TestMethod]
    [DataRow("/photos/a.txt/b.txt", DisplayName = "a file where a folder must be")]
    [DataRow("/photos/2026", DisplayName = "a folder at the name")]
    public async Task ServeAsync_PutConflictingWithAnEntryOfTheOtherKind_Is409InvalidObjectName(string target)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/a.txt", "a");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", target, "hello world");

        Assert.AreEqual(GcsServerHarness.ExpectedError("409 Conflict", "InvalidObjectName", "The specified object name is not valid."), response);
    }

    [TestMethod]
    public async Task ServeAsync_PutOfANameTheStoreCannotHold_Is400InvalidObjectName()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a/%2e%2e/b.txt", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidObjectName</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutRefusedByShareAccess_Is403AccessDenied()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var share = new ContentShare("shared", GcsServerHarness.Root, shareFileSystem)
        {
            Grants = new Dictionary<string, ContentShareAccess> { ["alice"] = ContentShareAccess.Read },
        };
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [share], accountName: "alice");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/shared/a.txt", "hello world");

        Assert.AreEqual(GcsServerHarness.ExpectedError("403 Forbidden", "AccessDenied", "Access denied."), response);
        Assert.AreEqual(ContentEntryKind.None, shareFileSystem.GetEntryKind(Path.Join(GcsServerHarness.Root, "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutInAFolderOfAReadOnlyShare_Is403AccessDenied()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var share = new ContentShare("shared", GcsServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [share]);

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/shared/2026/a.txt", "hello world");

        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutIntoAWritableShare_IsStoredWithTheFilesGeneration()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [new ContentShare("shared", GcsServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/shared/a.txt", "hello world");

        StringAssert.Contains(response, $"x-goog-generation: {GcsServerHarness.NowGeneration}\r\n");
        Assert.AreEqual(ContentEntryKind.File, shareFileSystem.GetEntryKind(Path.Join(GcsServerHarness.Root, "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutWhoseFileIsReplacedByAFolderWhileTheBodyIsRead_IsRefused()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var head = $"PUT /photos/a.txt HTTP/1.1\r\nHost: x\r\n{GcsServerHarness.BearerField}\r\nContent-Length: 11\r\nConnection: close\r\n\r\n";
        var connection = new UnitTestActingConnection(
            new InMemoryConnection([Encoding.Latin1.GetBytes(head), Encoding.Latin1.GetBytes("hello world")]),
            () => harness.CreateFolder("photos/a.txt"));

        await harness.Server.ServeAsync(connection, harness.Context());

        Assert.AreEqual(ContentEntryKind.Directory, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    [DataRow(HttpRequestBodyDiscardOutcome.TooLarge, false, "EntityTooLarge")]
    [DataRow(HttpRequestBodyDiscardOutcome.Discarded, true, "EntityTooLarge")]
    [DataRow(HttpRequestBodyDiscardOutcome.Incomplete, false, "IncompleteBody")]
    [DataRow(HttpRequestBodyDiscardOutcome.Discarded, false, null)]
    public void BodyRefusal_Outcome_IsTheErrorItCallsFor(HttpRequestBodyDiscardOutcome outcome, bool isTooLarge, string? code)
    {
        Assert.AreEqual(code, GcsPutObject.BodyRefusal(outcome, isTooLarge)?.Code);
    }

    [TestMethod]
    [DataRow(ObjectMetadataStoreOutcome.MetadataTooLarge, "MetadataTooLarge")]
    [DataRow(ObjectMetadataStoreOutcome.MetadataIndexFull, "InsufficientQuota")]
    [DataRow(ObjectMetadataStoreOutcome.Stored, null)]
    [DataRow(ObjectMetadataStoreOutcome.NoSuchFile, null)]
    public void MetadataRefusal_Outcome_IsTheErrorItCallsFor(ObjectMetadataStoreOutcome outcome, string? code)
    {
        Assert.AreEqual(code, GcsPutObject.MetadataRefusal(outcome)?.Code);
    }

    [TestMethod]
    public async Task ServeAsync_PutIntoAnExistingFolder_IsStored()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026/a.txt", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/2026/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutWhoseIndexFileCannotBeWritten_StoresTheObjectAndNotesTheFailure()
    {
        var harness = new GcsServerHarness(maxTotalBytes: 22, persistsIndex: true, exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsTrue(harness.Log.Entries.Any(entry => entry.Text.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_ZeroBytePut_StoresAnEmptyObject()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/empty.txt", string.Empty);

        StringAssert.Contains(response, "x-goog-hash: crc32c=AAAAAA==,md5=1B2M2Y8AsgTpgAmY7PhCfg==\r\n");
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/empty.txt"));
    }
}
