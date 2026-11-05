using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's PutObject, whose bytes are written from the S3 API Reference's "PutObject"
/// example and "Error responses" page, and from "Checking object integrity" and "Working with
/// object metadata" in the user guide (ADR-0108, decisions 1, 3 and 7).
/// </summary>
[TestClass]
public sealed class S3PutObjectTests
{
    // MD5("hello world"), hex and base64.
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";
    private const string HelloWorldContentMd5 = "XrY7u+Ae7tCTyyK7j1rNww==";

    // MD5 of no bytes.
    private const string EmptyETag = "\"d41d8cd98f00b204e9800998ecf8427e\"";

    [TestMethod]
    public async Task ServeAsync_PutObject_StoresTheBodyAndAnswers200WithItsETag()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world", "Content-Type: text/plain");

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + $"ETag: {HelloWorldETag}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutThenGet_ReadsBackTheBodyAndItsContentType()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world", "Content-Type: text/plain");

        var response = await harness.SendAsync("GET", "/photos/a.txt");

        StringAssert.Contains(response, $"ETag: {HelloWorldETag}\r\nAccept-Ranges: bytes\r\nContent-Type: text/plain\r\nContent-Length: 11\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nhello world");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectFromTheApiReferenceExample_IsVirtualHostedAndKeepsItsMetadataForHeadObject()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("mybucket");
        var body = new string('x', 11434);

        var put = await harness.SendRawAsync(
            "PUT /my-image.jpg HTTP/1.1\r\nHost: mybucket.s3.us-east-1.amazonaws.com\r\nDate: Wed, 12 Oct 2009 17:50:00 GMT\r\n"
                + "Content-Type: text/plain\r\nContent-Length: 11434\r\nx-amz-meta-author: Janet\r\n\r\n" + body);
        var head = await harness.SendAsync("HEAD", "/mybucket/my-image.jpg");

        StringAssert.StartsWith(put, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(head, "Content-Type: text/plain\r\nx-amz-meta-author: Janet\r\nContent-Length: 11434\r\n");
        Assert.AreEqual(body, harness.ReadFile("mybucket/my-image.jpg"));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithEveryStoredField_IsReadBackByHeadObject()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        await harness.SendWithBodyAsync(
            "PUT",
            "/photos/a.txt",
            "hello world",
            "Content-Encoding: gzip",
            "Content-Disposition: attachment",
            "Cache-Control: no-cache",
            "Content-Language: en",
            "X-Amz-Meta-Colour: blue",
            "x-amz-meta-colour: green");

        var head = await harness.SendAsync("HEAD", "/photos/a.txt");

        StringAssert.Contains(
            head,
            "Content-Type: application/octet-stream\r\nContent-Encoding: gzip\r\nContent-Disposition: attachment\r\n"
                + "Cache-Control: no-cache\r\nContent-Language: en\r\nx-amz-meta-colour: green\r\nContent-Length: 11\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithUploadsOff_Is403AccessDeniedAndStoresNothing()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "403 Forbidden",
                "<Code>AccessDenied</Code><Message>Access Denied</Message><Resource>/photos/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithAMatchingContentMd5_IsStored()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world", $"Content-MD5: {HelloWorldContentMd5}");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithABadContentMd5_Is400BadDigestAndKeepsTheOldObject()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "old");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world!", $"Content-MD5: {HelloWorldContentMd5}");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                "<Code>BadDigest</Code><Message>The Content-MD5 you specified did not match what we received.</Message><Resource>/photos/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
        Assert.AreEqual("old", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("not base64!", DisplayName = "not base64")]
    [DataRow("aGVsbG8=", DisplayName = "the base64 of 5 bytes")]
    public async Task ServeAsync_PutObjectWithAMalformedContentMd5_Is400InvalidDigest(string contentMd5)
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world", $"Content-MD5: {contentMd5}");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidDigest</Code><Message>The Content-MD5 you specified is not valid.</Message>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithAnEmptyBody_StoresAnEmptyObject()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("PUT", "/photos/empty.txt", "Content-Length: 0");

        StringAssert.Contains(response, $"ETag: {EmptyETag}\r\n");
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/empty.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectInFoldersThatDoNotExist_CreatesThem()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/2026/11/05/a.txt", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/2026/11/05/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutFolderObject_CreatesTheFolder()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("PUT", "/photos/2026/", "Content-Length: 0");

        StringAssert.Contains(response, $"ETag: {EmptyETag}\r\n");
        Assert.AreEqual(ContentEntryKind.Directory, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "photos", "2026")));
    }

    [TestMethod]
    public async Task ServeAsync_PutFolderObjectWithABody_Is400InvalidArgument()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/2026/", "x");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                "<Code>InvalidArgument</Code><Message>Invalid Argument</Message><ArgumentName>key</ArgumentName><ArgumentValue>2026/</ArgumentValue><Resource>/photos/2026/</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    [DataRow("/photos/a%2Fb", DisplayName = "an encoded separator")]
    [DataRow("/photos/a//b", DisplayName = "an empty segment")]
    [DataRow("/photos/con", DisplayName = "a reserved device name")]
    public async Task ServeAsync_PutObjectAtAKeyTheStoreCannotHold_Is400InvalidArgument(string target)
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", target, "x");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidArgument</Code><Message>Invalid Argument</Message><ArgumentName>key</ArgumentName>");
    }

    [TestMethod]
    [DataRow("/photos/a.txt/b.txt", DisplayName = "a file where a folder is named")]
    [DataRow("/photos/folder", DisplayName = "a folder where the file is named")]
    public async Task ServeAsync_PutObjectThatConflictsWithAnEntry_Is409InvalidArgument(string target)
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/folder");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBodyAsync("PUT", target, "x");

        StringAssert.StartsWith(response, "HTTP/1.1 409 Conflict\r\n");
        StringAssert.Contains(response, "<Code>InvalidArgument</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectToAHiddenFolder_Is403AccessDenied()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/.hidden/a.txt", "x");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectToAHiddenFile_Is403AccessDenied()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/.hidden", "x");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithNoContentLength_Is411MissingContentLength()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("PUT", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 411 Length Required\r\n");
        StringAssert.Contains(response, "<Code>MissingContentLength</Code><Message>You must provide the Content-Length HTTP header.</Message>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithAChunkedBody_Is400InvalidRequest()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendRawAsync("PUT /photos/a.txt HTTP/1.1\r\nHost: s3.test\r\nTransfer-Encoding: chunked\r\n\r\n1\r\nx\r\n0\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidRequest</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithAnAwsChunkedBody_Is501NotImplemented()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendRawAsync(
            "PUT /photos/a.txt HTTP/1.1\r\nHost: s3.test\r\nTransfer-Encoding: chunked\r\nx-amz-decoded-content-length: 1\r\n\r\n1\r\nx\r\n0\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 501 Not Implemented\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithAnUnreadableBody_Is400InvalidRequest()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendRawAsync("PUT /photos/a.txt HTTP/1.1\r\nHost: s3.test\r\nTransfer-Encoding: gzip\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidRequest</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectAnnouncedPastMaxFilesize_Is400EntityTooLargeAndCloses()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable with { MaxUploadBytes = 10 });
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>EntityTooLarge</Code><Message>Your proposed upload exceeds the maximum allowed object size.</Message>");
        StringAssert.Contains(response, "Connection: close\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public void BodyRefusal_EachReadingOutcome_HasItsError()
    {
        Assert.IsNull(S3PutObject.BodyRefusal(Surl.HttpServing.HttpRequestBodyDiscardOutcome.Discarded, isTooLarge: false));
        Assert.AreSame(S3Error.EntityTooLarge, S3PutObject.BodyRefusal(Surl.HttpServing.HttpRequestBodyDiscardOutcome.Discarded, isTooLarge: true));
        Assert.AreSame(S3Error.EntityTooLarge, S3PutObject.BodyRefusal(Surl.HttpServing.HttpRequestBodyDiscardOutcome.TooLarge, isTooLarge: false));
        Assert.AreSame(S3Error.IncompleteBody, S3PutObject.BodyRefusal(Surl.HttpServing.HttpRequestBodyDiscardOutcome.Incomplete, isTooLarge: false));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWhoseBodyEndsEarly_Is400IncompleteBody()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendRawAsync("PUT /photos/a.txt HTTP/1.1\r\nHost: s3.test\r\nContent-Length: 11\r\n\r\nhello");

        StringAssert.Contains(response, "<Code>IncompleteBody</Code><Message>You did not provide the number of bytes specified by the Content-Length HTTP header.</Message>");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithUserMetadataPast2KiB_Is400MetadataTooLarge()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "x", $"x-amz-meta-big: {new string('v', 2046)}");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>MetadataTooLarge</Code><Message>Your metadata headers exceed the maximum allowed metadata size.</Message>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWhoseMetadataPassesTheIndexBound_Is400MetadataTooLargeAndRemovesTheObject()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "x", $"Content-Disposition: {new string('d', 9000)}");

        StringAssert.Contains(response, "<Code>MetadataTooLarge</Code>");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithTheIndexFull_Is503ServiceUnavailable()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, maxIndexEntries: 0);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "x");

        StringAssert.StartsWith(response, "HTTP/1.1 503 Service Unavailable\r\n");
        StringAssert.Contains(response, "<Code>ServiceUnavailable</Code><Message>Service is unable to handle request.</Message>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithAnIndexThatCannotBeWritten_StoresTheObjectAndNotesTheFailure()
    {
        var harness = new S3ServerHarness(maxTotalBytes: 20, persistsIndex: true, exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectToAMissingBucket_Is404NoSuchBucket()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "x");

        StringAssert.Contains(response, "<Code>NoSuchBucket</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithAKeyOver1024Bytes_Is400KeyTooLongError()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", $"/photos/{new string('k', 1025)}", "x");

        StringAssert.Contains(response, "<Code>KeyTooLongError</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_CopyObject_Is501NotImplemented()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("PUT", "/photos/b.txt", "x-amz-copy-source: /photos/a.txt", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 501 Not Implemented\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectInAShare_IsStoredWithTheBodysETagAndReadBackWithTheDefaults()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, shares: [new ContentShare("shared", S3ServerHarness.Root, shareFileSystem)]);

        var put = await harness.SendWithBodyAsync("PUT", "/shared/a.txt", "hello world", "Content-Type: text/plain");
        var get = await harness.SendAsync("GET", "/shared/a.txt");

        StringAssert.Contains(put, $"ETag: {HelloWorldETag}\r\n");
        StringAssert.Contains(get, $"ETag: {HelloWorldETag}\r\nAccept-Ranges: bytes\r\nContent-Type: application/octet-stream\r\n");
        Assert.AreEqual(0, harness.Index.Count);
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectToAReadOnlyShare_Is403AccessDenied()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        var share = new ContentShare("shared", S3ServerHarness.Root, shareFileSystem)
        {
            Grants = new Dictionary<string, ContentShareAccess> { [ContentShare.EveryoneMember] = ContentShareAccess.Read },
        };
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, shares: [share]);

        var response = await harness.SendWithBodyAsync("PUT", "/shared/a.txt", "hello world");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "403 Forbidden",
                "<Code>AccessDenied</Code><Message>Access Denied</Message><Resource>/shared/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
        Assert.AreEqual(ContentEntryKind.None, shareFileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectInAFolderOfAReadOnlyShare_Is403AccessDenied()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        var share = new ContentShare("shared", S3ServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, shares: [share]);

        var response = await harness.SendWithBodyAsync("PUT", "/shared/2026/a.txt", "hello world");

        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectBehindABucketName_KeepsItsMetadataUnderTheContentPath()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, buckets: [new ContentBucket("media", "/store/photos")]);
        harness.CreateFolder("store");
        harness.CreateFolder("store/photos");

        await harness.SendWithBodyAsync("PUT", "/media/a.txt", "hello world", "Content-Type: text/plain");
        var head = await harness.SendAsync("HEAD", "/media/a.txt");

        Assert.AreEqual("hello world", harness.ReadFile("store/photos/a.txt"));
        StringAssert.Contains(head, "Content-Type: text/plain\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_FolderAppearingAtTheKeyWhileTheBodyIsRead_Is403AccessDenied()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var inner = new InMemoryConnection([
            Encoding.Latin1.GetBytes("PUT /photos/a.txt HTTP/1.1\r\nHost: s3.test\r\nContent-Length: 11\r\nConnection: close\r\n\r\n"),
            Encoding.Latin1.GetBytes("hello world")]);
        var connection = new UnitTestActingConnection(inner, () => harness.CreateFolder("photos/a.txt"));

        await harness.Server.ServeAsync(connection, harness.Context());

        StringAssert.StartsWith(Encoding.UTF8.GetString(inner.WrittenBytes), "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithNoUploadLimit_IsStored()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable with { MaxUploadBytes = 0 });
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public void CommitRefusal_EachUploadResult_IsAnsweredAsADeniedWriteUnlessWritten()
    {
        Assert.IsNull(S3PutObject.CommitRefusal(ContentUploadResult.Written));
        Assert.AreSame(S3Error.AccessDenied, S3PutObject.CommitRefusal(ContentUploadResult.NotPermitted));
        Assert.AreSame(S3Error.AccessDenied, S3PutObject.CommitRefusal(ContentUploadResult.TooLarge));
    }

    [TestMethod]
    public void MetadataRefusal_EachStoreOutcome_HasItsError()
    {
        Assert.IsNull(S3PutObject.MetadataRefusal(ObjectMetadataStoreOutcome.Stored));
        Assert.IsNull(S3PutObject.MetadataRefusal(ObjectMetadataStoreOutcome.NoSuchFile));
        Assert.AreSame(S3Error.MetadataTooLarge, S3PutObject.MetadataRefusal(ObjectMetadataStoreOutcome.MetadataTooLarge));
        Assert.AreSame(S3Error.ServiceUnavailable, S3PutObject.MetadataRefusal(ObjectMetadataStoreOutcome.MetadataIndexFull));
    }

    [TestMethod]
    public void UploadStream_EverythingButAnAsynchronousWrite_IsNotSupported()
    {
        using var md5 = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.MD5);
        using var stream = new S3UploadStream(null!, md5);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        stream.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [TestMethod]
    public async Task UploadStream_ArrayWrite_IsAppendedToTheSession()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        var mapping = harness.Store.MapRequestPath("/a.txt");
        var opening = await harness.Store.OpenUploadAsync(mapping, new ContentUploadOpening(false, true, false), CancellationToken.None);
        await using var session = opening.Session!;
        using var md5 = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.MD5);
        await using var stream = new S3UploadStream(session, md5);

        await stream.WriteAsync(Encoding.UTF8.GetBytes("hello world"), 0, 11, CancellationToken.None);

        Assert.AreEqual(11, session.Length);
        Assert.IsFalse(stream.IsTooLarge);
    }

    [TestMethod]
    public async Task UploadStream_WritesPastTheUploadLimit_AreTooLargeAndTheRestDropped()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable with { MaxUploadBytes = 4 });
        var mapping = harness.Store.MapRequestPath("/a.txt");
        var opening = await harness.Store.OpenUploadAsync(mapping, new ContentUploadOpening(false, true, false), CancellationToken.None);
        await using var session = opening.Session!;
        using var md5 = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.MD5);
        await using var stream = new S3UploadStream(session, md5);

        await stream.WriteAsync(Encoding.UTF8.GetBytes("hello"), CancellationToken.None);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(" world"), CancellationToken.None);

        Assert.IsTrue(stream.IsTooLarge);
        CollectionAssert.AreEqual(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes("hello world")), md5.GetHashAndReset());
    }
}
