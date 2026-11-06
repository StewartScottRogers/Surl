using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsUploadRequests;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Completing a multipart upload, from the XML API page "POST Object complete multipart"
/// (ADR-0110, decision 9).
/// </summary>
[TestClass]
public sealed class GcsCompleteMultipartUploadTests
{
    private const string LastPart = "the last part";
    private static readonly string FirstPart = new('a', FiveMebibytes);
    private static readonly string SecondPart = new('b', FiveMebibytes);

    [TestMethod]
    public async Task ServeAsync_ThreePartsTheFirstTwo5MiB_AreAssembledAndReadBackWithGetObject()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/big.bin", "Content-Type: image/png", "x-goog-meta-author: Janet");
        var eTags = new[]
        {
            ETagFieldOf(await UploadPartAsync(harness, "/photos/big.bin", uploadId, 1, FirstPart)),
            ETagFieldOf(await UploadPartAsync(harness, "/photos/big.bin", uploadId, 2, SecondPart)),
            ETagFieldOf(await UploadPartAsync(harness, "/photos/big.bin", uploadId, 3, LastPart)),
        };
        var expectedETag = $"\"{Convert.ToHexStringLower(MD5.HashData([.. new[] { FirstPart, SecondPart, LastPart }.SelectMany(part => MD5.HashData(Encoding.UTF8.GetBytes(part)))]))}-3\"";

        var response = await CompleteAsync(harness, "/photos/big.bin", uploadId, CompletionBody((1, eTags[0]), (2, eTags[1]), (3, eTags[2])));
        var read = await harness.SendWithBearerAsync("GET", "/photos/big.bin");

        CollectionAssert.AreEqual(new[] { ETagOf(FirstPart), ETagOf(SecondPart), ETagOf(LastPart) }, eTags);
        var body = "<?xml version='1.0' encoding='UTF-8'?><CompleteMultipartUploadResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">"
            + $"<Location>http://127.0.0.1:4443/photos/big.bin</Location><Bucket>photos</Bucket><Key>big.bin</Key><ETag>{expectedETag}</ETag></CompleteMultipartUploadResult>";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}Content-Type: application/xml; charset=UTF-8\r\n"
                + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}",
            response);
        StringAssert.Contains(read, "Content-Type: image/png\r\n");
        StringAssert.Contains(read, $"ETag: {expectedETag}\r\n");
        StringAssert.Contains(read, "x-goog-meta-author: Janet\r\n");
        StringAssert.EndsWith(read, FirstPart + SecondPart + LastPart);
        Assert.HasCount(1, harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(GcsServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task ServeAsync_AfterCompletion_TheUploadIsGone()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 7, "hello");
        await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((7, ETagOf("hello").ToUpperInvariant().Trim('"'))));

        var again = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((7, ETagOf("hello"))));

        Assert.AreEqual("hello", harness.ReadFile("photos/a.txt"));
        StringAssert.Contains(again, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_OverTlsWithNoHost_LocatesTheObjectOnTheListenHostOverHttps()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");
        var body = CompletionBody((1, ETagOf("hello")));

        var response = await harness.SendRawOverTlsAsync($"POST /photos/a.txt?uploadId={uploadId} HTTP/1.0\r\n{GcsServerHarness.BearerField}\r\nContent-Length: {body.Length}\r\n\r\n{body}");

        StringAssert.Contains(response, "<Location>https://127.0.0.1/photos/a.txt</Location>");
    }

    [TestMethod]
    public async Task ServeAsync_PartsOutOfOrder_Is400InvalidPartOrderAndTheUploadStaysOpen()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "one");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "two");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((2, ETagOf("two")), (1, ETagOf("one"))));
        var parts = await ListPartsAsync(harness, "/photos/a.txt", uploadId);

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidPartOrder", "The list of parts was not in ascending order."), response);
        StringAssert.StartsWith(parts, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    [DataRow(2, "one", DisplayName = "a part not uploaded")]
    [DataRow(1, "two", DisplayName = "another ETag")]
    public async Task ServeAsync_APartNotUploadedAsNamed_Is400InvalidPart(int partNumber, string eTagOf)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "one");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((partNumber, ETagOf(eTagOf))));

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidPart", "One or more of the specified parts could not be found."), response);
    }

    [TestMethod]
    public async Task ServeAsync_APartButTheLastBelow5MiB_Is400EntityTooSmall()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "one");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "two");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("one")), (2, ETagOf("two"))));

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "EntityTooSmall", "Your proposed upload is smaller than the minimum allowed object size."), response);
    }

    [TestMethod]
    [DataRow("<CompleteMultipartUpload/>", DisplayName = "no part")]
    [DataRow("not xml", DisplayName = "not XML")]
    public async Task ServeAsync_AMalformedBody_Is400InvalidArgument(string body)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, body);

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidArgument", "Invalid argument."), response);
    }

    [TestMethod]
    public async Task ServeAsync_ABodyPast1MiB_Is400InvalidArgument()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "a");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("a"))) + new string(' ', GcsCompleteMultipartUploadDocument.MaxBodyBytes));

        StringAssert.Contains(response, "<Code>InvalidArgument</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_NoBody_Is400InvalidArgument()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendWithBearerAsync("POST", $"/photos/a.txt?uploadId={uploadId}");

        StringAssert.Contains(response, "<Code>InvalidArgument</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ABodyThatEndsEarly_Is400IncompleteBody()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendRawAsync($"POST /photos/a.txt?uploadId={uploadId} HTTP/1.1\r\nHost: x\r\n{GcsServerHarness.BearerField}\r\nContent-Length: 200\r\n\r\n<Complete");

        StringAssert.Contains(response, "<Code>IncompleteBody</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AFolderAtTheNameByCompletion_Is409InvalidObjectName()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "a");
        harness.CreateFolder("photos/a.txt");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("a"))));

        StringAssert.StartsWith(response, "HTTP/1.1 409 Conflict\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_CompletionWhenTheIndexIsFull_Is403InsufficientQuota()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, maxIndexEntries: 0);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "a");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("a"))));

        StringAssert.Contains(response, "<Code>InsufficientQuota</Code>");
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_SignedCompletionWhoseBodyIsNotTheOneSigned_Is400BadDigest()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.Policy.SignedAccounts[GcsSignedRequests.PutAuthorization] = GcsSignedRequests.AccessId;
        harness.CreateFolder("example-bucket");
        var uploadId = await InitiateUploadIdAsync(harness, "/example-bucket/hello.txt");
        var request = GcsSignedRequests.Request("PUT", "hello WORLD").Replace("PUT /example-bucket/hello.txt ", $"POST /example-bucket/hello.txt?uploadId={uploadId} ", StringComparison.Ordinal);

        var response = await harness.SendRawAsync(request);

        StringAssert.Contains(response, "<Code>BadDigest</Code>");
    }
}
