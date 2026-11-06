using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using static Surl.Extended.Protocol.S3.S3MultipartRequests;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's CompleteMultipartUpload, whose bytes are written from the S3 API Reference's
/// "CompleteMultipartUpload" page and the user guide's "Checking object integrity" (ADR-0108,
/// decisions 1, 3 and 7).
/// </summary>
[TestClass]
public sealed class S3CompleteMultipartUploadTests
{
    private static readonly string FirstPart = new('a', FiveMebibytes);
    private static readonly string SecondPart = new('b', FiveMebibytes);
    private const string LastPart = "the last part";

    [TestMethod]
    public async Task ServeAsync_ThreePartsTheFirstTwo5MiB_CompleteIntoOneObjectWhoseETagEndsDash3()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/big.bin", "Content-Type: image/png", "x-amz-meta-author: Janet");
        var eTags = new[]
        {
            ETagFieldOf(await UploadPartAsync(harness, "/photos/big.bin", uploadId, 1, FirstPart)),
            ETagFieldOf(await UploadPartAsync(harness, "/photos/big.bin", uploadId, 2, SecondPart)),
            ETagFieldOf(await UploadPartAsync(harness, "/photos/big.bin", uploadId, 3, LastPart)),
        };
        var expectedETag = $"\"{Convert.ToHexStringLower(MD5.HashData([.. new[] { FirstPart, SecondPart, LastPart }.SelectMany(part => MD5.HashData(Encoding.UTF8.GetBytes(part)))]))}-3\"";

        var response = await CompleteAsync(harness, "/photos/big.bin", uploadId, CompletionBody((1, eTags[0]), (2, eTags[1]), (3, eTags[2])));
        var head = await harness.SendAsync("HEAD", "/photos/big.bin");

        CollectionAssert.AreEqual(new[] { ETagOf(FirstPart), ETagOf(SecondPart), ETagOf(LastPart) }, eTags);
        var body = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<CompleteMultipartUploadResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">"
            + $"<Location>http://s3.test/photos/big.bin</Location><Bucket>photos</Bucket><Key>big.bin</Key><ETag>{expectedETag}</ETag></CompleteMultipartUploadResult>";
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + $"Content-Type: application/xml\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}",
            response);
        StringAssert.EndsWith(expectedETag, "-3\"");
        StringAssert.Contains(head, $"ETag: {expectedETag}\r\n");
        StringAssert.Contains(head, "Content-Type: image/png\r\nx-amz-meta-author: Janet\r\n");
        Assert.AreEqual(FirstPart + SecondPart + LastPart, harness.ReadFile("photos/big.bin"));
        Assert.HasCount(1, harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(S3ServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task ServeAsync_OnePartOnly_MayBeSmallAndHasTheETagDash1()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 7, "hello");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((7, ETagOf("hello").ToUpperInvariant().Trim('"'))));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(response, $"-1\"</ETag>");
        Assert.AreEqual("hello", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AfterCompletion_TheUploadIsGone()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");
        await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("hello"))));

        var again = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("hello"))));

        StringAssert.StartsWith(again, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_OverTlsWithNoHost_LocatesTheObjectOnTheListenHostOverHttps()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");
        var body = CompletionBody((1, ETagOf("hello")));

        var response = await harness.SendRawOverTlsAsync($"POST /photos/a.txt?uploadId={uploadId} HTTP/1.0\r\nContent-Length: {body.Length}\r\n\r\n{body}");

        StringAssert.Contains(response, "<Location>https://127.0.0.1/photos/a.txt</Location>");
    }

    [TestMethod]
    public async Task ServeAsync_PartsOutOfOrder_Is400InvalidPartOrderAndTheUploadStaysOpen()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, FirstPart);
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "end");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((2, ETagOf("end")), (1, ETagOf(FirstPart))));
        var retried = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf(FirstPart)), (2, ETagOf("end"))));

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                "<Code>InvalidPartOrder</Code><Message>The list of parts was not in ascending order. The parts list must be specified in order by part number.</Message>"
                    + $"<Resource>/photos/a.txt</Resource><RequestId>{{RequestId}}</RequestId>"),
            response);
        StringAssert.StartsWith(retried, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_APartNamedTwice_Is400InvalidPartOrder()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, FirstPart);

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf(FirstPart)), (1, ETagOf(FirstPart))));

        StringAssert.Contains(response, "<Code>InvalidPartOrder</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_APartNotUploaded_Is400InvalidPartAndWritesNothing()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("hello")), (2, ETagOf("hello"))));

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                "<Code>InvalidPart</Code><Message>One or more of the specified parts could not be found. The part might not have been uploaded, or the specified entity tag might not have matched the part's entity tag.</Message>"
                    + $"<Resource>/photos/a.txt</Resource><RequestId>{{RequestId}}</RequestId>"),
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_APartWithAnotherETag_Is400InvalidPart()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("other"))));

        StringAssert.Contains(response, "<Code>InvalidPart</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_APartUploadedAgain_ReplacesTheOldOne()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "first");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "second");

        var old = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("first"))));
        var replaced = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("second"))));

        StringAssert.Contains(old, "<Code>InvalidPart</Code>");
        StringAssert.StartsWith(replaced, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("second", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_APartButTheLastBelow5MiB_Is400EntityTooSmall()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "small");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "end");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("small")), (2, ETagOf("end"))));

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                "<Code>EntityTooSmall</Code><Message>Your proposed upload is smaller than the minimum allowed object size.</Message>"
                    + $"<Resource>/photos/a.txt</Resource><RequestId>{{RequestId}}</RequestId>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ABodyThatDoesNotParse_Is400MalformedXml()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, "<CompleteMultipartUpload>");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                "<Code>MalformedXML</Code><Message>The XML you provided was not well-formed or did not validate against our published schema.</Message>"
                    + $"<Resource>/photos/a.txt</Resource><RequestId>{{RequestId}}</RequestId>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_AnEmptyBody_Is400MalformedXml()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, string.Empty);

        StringAssert.Contains(response, "<Code>MalformedXML</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ABodyPast1MiB_Is400MalformedXml()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("a"))) + new string(' ', S3CompleteMultipartUploadDocument.MaxBodyBytes));

        StringAssert.Contains(response, "<Code>MalformedXML</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ABodyCutShort_Is400IncompleteBody()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendRawAsync($"POST /photos/a.txt?uploadId={uploadId} HTTP/1.1\r\nHost: s3.test\r\nContent-Length: 100\r\n\r\n<Complete");

        StringAssert.Contains(response, "<Code>IncompleteBody</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ASignedBodyNotTheSignedHash_Is400XAmzContentSha256Mismatch()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");
        var body = CompletionBody((1, ETagOf("hello")));

        var refused = await CompleteAsync(harness, "/photos/a.txt", uploadId, body, "Authorization: AWS4-HMAC-SHA256 signed", $"x-amz-content-sha256: {UnitTestS3AuthenticationPolicy.ContentSha256Of("other")}");
        var accepted = await CompleteAsync(harness, "/photos/a.txt", uploadId, body, "Authorization: AWS4-HMAC-SHA256 signed", $"x-amz-content-sha256: {UnitTestS3AuthenticationPolicy.ContentSha256Of(body)}");

        StringAssert.Contains(refused, "<Code>XAmzContentSHA256Mismatch</Code>");
        StringAssert.StartsWith(accepted, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AnUnknownUploadId_Is404NoSuchUpload()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await CompleteAsync(harness, "/photos/a.txt", "0123456789abcdef0123456789abcdef", CompletionBody((1, ETagOf("a"))));

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "404 Not Found",
                "<Code>NoSuchUpload</Code><Message>The specified upload does not exist. The upload ID may be invalid, or the upload may have been aborted or completed.</Message>"
                    + "<UploadId>0123456789abcdef0123456789abcdef</UploadId><Resource>/photos/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_UploadsOff_Is403AccessDenied()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await CompleteAsync(harness, "/photos/a.txt", "0123456789abcdef0123456789abcdef", CompletionBody((1, ETagOf("a"))));

        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AFolderMadeAtTheKeyMeanwhile_Is409InvalidArgumentAndTheUploadStaysOpen()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");
        harness.CreateFolder("photos/a.txt");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("hello"))));
        var abort = await AbortAsync(harness, "/photos/a.txt", uploadId);

        StringAssert.StartsWith(response, "HTTP/1.1 409 Conflict\r\n");
        StringAssert.StartsWith(abort, "HTTP/1.1 204 No Content\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_TheMetadataIndexFull_Is503ServiceUnavailableAndTheObjectIsRemoved()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, maxIndexEntries: 0);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("hello"))));

        StringAssert.Contains(response, "<Code>ServiceUnavailable</Code>");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "photos", "a.txt")));
    }
}
