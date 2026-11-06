using System.Security.Cryptography;
using System.Text;
using Surl.Content;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The bodies PutObject and UploadPart read (ADR-0108 decision 4.5): <c>aws-chunked</c> bodies, as
/// "Signature Calculations for the Authorization Header: Transferring Payload in Multiple Chunks
/// (Chunked Upload)" sends its worked example, and the additional checksums of "Checking object
/// integrity". The worked example's chunk signatures are checked by
/// <see cref="UnitTestChunkSignatureChain"/>, which accepts each only over the data's SHA-256 the
/// example signed; the signature arithmetic itself is tested in
/// <c>Surl.Authentication.UnitTests</c> (<c>AwsSigV4PresignedAndChunkedTests</c>).
/// </summary>
[TestClass]
public sealed class S3ObjectBodyReaderTests
{
    private const string FirstChunkSignature = "ad80c730a21e5b8d04586a2213dd63b9a0e99e0e2307b0ade35a65485a288648";
    private const string SecondChunkSignature = "0055627c9e194cb4542bae2aa5492e3c1575bbb81b612b7d234b86a503ef5497";
    private const string FinalChunkSignature = "b6c6ea8a5354eaf15b3cb7646744f4275b71ea724fed81ceb9323e279d449df9";

    // "hello world" under each algorithm, base64 as the x-amz-checksum-* fields carry it.
    private const string HelloWorldCrc32 = "DUoRhQ==";
    private const string HelloWorldCrc32C = "yZRlqg==";
    private const string HelloWorldSha1 = "Kq5sNclPz7QV2+lfQIuc6R7oRu0=";
    private const string HelloWorldSha256 = "uU0nuZNNPgilLlLX2n2r+sSE7+N6U4DukIj3rOLvzek=";

    private static readonly byte[] FirstChunk = [.. Enumerable.Repeat((byte)'a', 65536)];
    private static readonly byte[] SecondChunk = [.. Enumerable.Repeat((byte)'a', 1024)];

    private static UnitTestChunkSignatureChain WorkedExampleChain() => new(
        (SHA256.HashData(FirstChunk), FirstChunkSignature),
        (SHA256.HashData(SecondChunk), SecondChunkSignature),
        (SHA256.HashData([]), FinalChunkSignature));

    private static S3ServerHarness Harness(UnitTestChunkSignatureChain? chain = null, long maxUploadBytes = 0)
    {
        var harness = new S3ServerHarness(
            exposure: S3ServerHarness.Writable with { MaxUploadBytes = maxUploadBytes },
            policy: new UnitTestS3AuthenticationPolicy { ChunkSignatures = chain });
        harness.CreateFolder("examplebucket");

        return harness;
    }

    // The worked example's request, its chunks as given or with the first one's data replaced.
    private static string WorkedExample(byte[]? firstChunk = null) =>
        "PUT /examplebucket/chunkObject.txt HTTP/1.1\r\nHost: s3.amazonaws.com\r\nx-amz-date: 20130524T000000Z\r\n"
        + "x-amz-storage-class: REDUCED_REDUNDANCY\r\n"
        + "Authorization: AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,"
        + "SignedHeaders=content-encoding;content-length;host;x-amz-content-sha256;x-amz-date;x-amz-decoded-content-length;x-amz-storage-class,"
        + "Signature=4f232c4386841ef735655705268965c44a0e4690baa4adea153f7db9fa80a0a9\r\n"
        + "x-amz-content-sha256: STREAMING-AWS4-HMAC-SHA256-PAYLOAD\r\nContent-Encoding: aws-chunked\r\n"
        + "x-amz-decoded-content-length: 66560\r\nContent-Length: 66824\r\nConnection: close\r\n\r\n"
        + $"10000;chunk-signature={FirstChunkSignature}\r\n{Encoding.Latin1.GetString(firstChunk ?? FirstChunk)}\r\n"
        + $"400;chunk-signature={SecondChunkSignature}\r\n{Encoding.Latin1.GetString(SecondChunk)}\r\n"
        + $"0;chunk-signature={FinalChunkSignature}\r\n\r\n";

    // An unsigned-trailer PUT of "hello world" in two chunks, with the given trailer line (none when empty).
    private static string TrailerPut(string target, string trailerFieldName, string trailer)
    {
        var body = $"6\r\nhello \r\n5\r\nworld\r\n0\r\n{(trailer.Length == 0 ? string.Empty : trailer + "\r\n")}\r\n";

        return $"PUT {target} HTTP/1.1\r\nHost: s3.test\r\nAuthorization: AWS4-HMAC-SHA256 signed\r\n"
            + $"x-amz-content-sha256: STREAMING-UNSIGNED-PAYLOAD-TRAILER\r\nContent-Encoding: aws-chunked\r\nx-amz-trailer: {trailerFieldName}\r\n"
            + $"x-amz-decoded-content-length: 11\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
    }

    private static string Md5ETag(byte[] bytes) => $"\"{Convert.ToHexStringLower(MD5.HashData(bytes))}\"";

    [TestMethod]
    public async Task ServeAsync_ChunkedUploadWorkedExample_StoresTheDecodedObjectAndChecksEveryChunk()
    {
        var chain = WorkedExampleChain();
        var harness = Harness(chain);
        byte[] decoded = [.. FirstChunk, .. SecondChunk];

        var response = await harness.SendRawAsync(WorkedExample());

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + $"ETag: {Md5ETag(decoded)}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual(Encoding.Latin1.GetString(decoded), harness.ReadFile("examplebucket/chunkObject.txt"));
        Assert.HasCount(3, chain.Asked);
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedUploadWorkedExample_KeepsNoAwsChunkedContentEncoding()
    {
        var harness = Harness(WorkedExampleChain());
        await harness.SendRawAsync(WorkedExample());

        var head = await harness.SendAsync("HEAD", "/examplebucket/chunkObject.txt");

        StringAssert.StartsWith(head, "HTTP/1.1 200 OK\r\n");
        Assert.IsFalse(head.Contains("Content-Encoding", StringComparison.Ordinal), head);
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedUploadWithATamperedChunk_Is403SignatureDoesNotMatchAndNotStored()
    {
        var harness = Harness(WorkedExampleChain());
        var tampered = FirstChunk.ToArray();
        tampered[1000] = (byte)'b';

        var response = await harness.SendRawAsync(WorkedExample(tampered));

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "403 Forbidden",
                "<Code>SignatureDoesNotMatch</Code><Message>The request signature we calculated does not match the signature you provided. Check your key and signing method.</Message>"
                    + "<Resource>/examplebucket/chunkObject.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "examplebucket/chunkObject.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_SignedChunksWithNoChainFromTheLogin_Is403SignatureDoesNotMatchBeforeTheBody()
    {
        var harness = Harness(chain: null);

        var response = await harness.SendRawAsync(WorkedExample());

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        StringAssert.Contains(response, "<Code>SignatureDoesNotMatch</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedUploadWithoutADecodedLength_Is411MissingContentLength()
    {
        var harness = Harness(WorkedExampleChain());

        var response = await harness.SendRawAsync(WorkedExample().Replace("x-amz-decoded-content-length: 66560\r\n", string.Empty, StringComparison.Ordinal));

        StringAssert.StartsWith(response, "HTTP/1.1 411 Length Required\r\n");
        StringAssert.Contains(response, "<Code>MissingContentLength</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedUploadWhoseDecodedLengthIsPastTheUploadLimit_Is400EntityTooLarge()
    {
        var harness = Harness(WorkedExampleChain(), maxUploadBytes: 66559);

        var response = await harness.SendRawAsync(WorkedExample());

        StringAssert.Contains(response, "<Code>EntityTooLarge</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedUploadOfExactlyTheUploadLimit_IsStored()
    {
        var harness = Harness(WorkedExampleChain(), maxUploadBytes: 66560);

        var response = await harness.SendRawAsync(WorkedExample());

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("STREAMING-AWS4-ECDSA-P256-SHA256-PAYLOAD")]
    [DataRow("STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER")]
    [DataRow("STREAMING-AWS4-ECDSA-P256-SHA256-PAYLOAD-TRAILER")]
    public async Task ServeAsync_AnotherStreamingForm_Is501NotImplemented(string contentSha256)
    {
        var harness = Harness();

        var response = await harness.SendRawAsync(TrailerPut("/examplebucket/a.txt", "x-amz-checksum-crc32", $"x-amz-checksum-crc32:{HelloWorldCrc32}")
            .Replace("STREAMING-UNSIGNED-PAYLOAD-TRAILER", contentSha256, StringComparison.Ordinal));

        StringAssert.StartsWith(response, "HTTP/1.1 501 Not Implemented\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_UnsignedTrailerWithItsCrc32_IsStoredAndTheChecksumEchoed()
    {
        var harness = Harness();

        var response = await harness.SendRawAsync(TrailerPut("/examplebucket/a.txt", "x-amz-checksum-crc32", $"x-amz-checksum-crc32:{HelloWorldCrc32}"));

        StringAssert.Contains(response, $"\r\nETag: {Md5ETag(Encoding.UTF8.GetBytes("hello world"))}\r\nx-amz-checksum-crc32: {HelloWorldCrc32}\r\nContent-Length: 0\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("examplebucket/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_UnsignedTrailerWithAnotherCrc32_Is400BadDigestAndNotStored()
    {
        var harness = Harness();

        var response = await harness.SendRawAsync(TrailerPut("/examplebucket/a.txt", "x-amz-checksum-crc32", "x-amz-checksum-crc32:AAAAAA=="));

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                "<Code>BadDigest</Code><Message>The CRC32 you specified did not match the calculated checksum.</Message>"
                    + "<Resource>/examplebucket/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "examplebucket/a.txt")));
    }

    [TestMethod]
    [DataRow("x-amz-meta-other:1", DisplayName = "the named checksum missing")]
    [DataRow("x-amz-checksum-crc32:not base64", DisplayName = "a value that is no digest")]
    public async Task ServeAsync_UnsignedTrailerWithoutAReadableChecksum_Is400InvalidRequest(string trailer)
    {
        var harness = Harness();

        var response = await harness.SendRawAsync(TrailerPut("/examplebucket/a.txt", "x-amz-checksum-crc32", trailer));

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidRequest</Code><Message>Value for x-amz-checksum header is invalid.</Message>");
    }

    [TestMethod]
    public async Task ServeAsync_UnsignedTrailerWithNoChecksumNamed_IsStored()
    {
        var harness = Harness();
        var request = TrailerPut("/examplebucket/a.txt", "x", string.Empty).Replace("x-amz-trailer: x\r\n", string.Empty, StringComparison.Ordinal);

        var response = await harness.SendRawAsync(request);

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("examplebucket/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_UnsignedTrailerWhoseDataIsShortOfTheDecodedLength_Is400IncompleteBody()
    {
        var harness = Harness();
        var request = TrailerPut("/examplebucket/a.txt", "x-amz-checksum-crc32", $"x-amz-checksum-crc32:{HelloWorldCrc32}")
            .Replace("x-amz-decoded-content-length: 11", "x-amz-decoded-content-length: 12", StringComparison.Ordinal);

        var response = await harness.SendRawAsync(request);

        StringAssert.Contains(response, "<Code>IncompleteBody</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AwsChunkedUnderHttpChunkedFraming_IsDecodedAndStored()
    {
        var harness = Harness();
        var awsChunked = $"b\r\nhello world\r\n0\r\nx-amz-checksum-crc32:{HelloWorldCrc32}\r\n\r\n";

        var response = await harness.SendRawAsync(
            "PUT /examplebucket/a.txt HTTP/1.1\r\nHost: s3.test\r\nAuthorization: AWS4-HMAC-SHA256 signed\r\n"
            + "x-amz-content-sha256: STREAMING-UNSIGNED-PAYLOAD-TRAILER\r\nx-amz-trailer: x-amz-checksum-crc32\r\n"
            + "x-amz-decoded-content-length: 11\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n"
            + $"{awsChunked.Length:x}\r\n{awsChunked}\r\n0\r\n\r\n");

        StringAssert.Contains(response, $"x-amz-checksum-crc32: {HelloWorldCrc32}\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("examplebucket/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AwsChunkedUnderAnUnreadableTransferCoding_Is400InvalidRequest()
    {
        var harness = Harness();

        var response = await harness.SendRawAsync(
            "PUT /examplebucket/a.txt HTTP/1.1\r\nHost: s3.test\r\nAuthorization: AWS4-HMAC-SHA256 signed\r\n"
            + "x-amz-content-sha256: STREAMING-UNSIGNED-PAYLOAD-TRAILER\r\nx-amz-decoded-content-length: 11\r\nTransfer-Encoding: gzip\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidRequest</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AwsChunkedWithNoBodyAnnounced_Is400IncompleteBody()
    {
        var harness = Harness();

        var response = await harness.SendRawAsync(
            "PUT /examplebucket/a.txt HTTP/1.1\r\nHost: s3.test\r\nAuthorization: AWS4-HMAC-SHA256 signed\r\n"
            + "x-amz-content-sha256: STREAMING-UNSIGNED-PAYLOAD-TRAILER\r\nx-amz-decoded-content-length: 0\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");

        StringAssert.Contains(response, "<Code>IncompleteBody</Code>");
    }

    [TestMethod]
    [DataRow("x-amz-checksum-crc32", HelloWorldCrc32)]
    [DataRow("x-amz-checksum-crc32c", HelloWorldCrc32C)]
    [DataRow("x-amz-checksum-sha1", HelloWorldSha1)]
    [DataRow("x-amz-checksum-sha256", HelloWorldSha256)]
    public async Task ServeAsync_PutObjectWithItsChecksumField_IsStoredAndTheChecksumEchoed(string fieldName, string value)
    {
        var harness = Harness();

        var response = await harness.SendWithBodyAsync("PUT", "/examplebucket/a.txt", "hello world", $"{fieldName}: {value}");

        StringAssert.Contains(response, $"\r\n{fieldName}: {value}\r\nContent-Length: 0\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("examplebucket/a.txt"));
    }

    [TestMethod]
    [DataRow("x-amz-checksum-crc32", HelloWorldCrc32, "CRC32")]
    [DataRow("x-amz-checksum-crc32c", HelloWorldCrc32C, "CRC32C")]
    [DataRow("x-amz-checksum-sha1", HelloWorldSha1, "SHA1")]
    [DataRow("x-amz-checksum-sha256", HelloWorldSha256, "SHA256")]
    public async Task ServeAsync_PutObjectWithAnotherBodysChecksum_Is400BadDigestNamingTheAlgorithm(string fieldName, string value, string algorithmName)
    {
        var harness = Harness();

        var response = await harness.SendWithBodyAsync("PUT", "/examplebucket/a.txt", "hello world!", $"{fieldName}: {value}");

        StringAssert.Contains(response, $"<Code>BadDigest</Code><Message>The {algorithmName} you specified did not match the calculated checksum.</Message>");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "examplebucket/a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithCrc64Nvme_Is501NotImplementedAndNotStored()
    {
        var harness = Harness();

        var response = await harness.SendWithBodyAsync("PUT", "/examplebucket/a.txt", "hello world", "x-amz-checksum-crc64nvme: AAAAAAAAAAA=");

        StringAssert.StartsWith(response, "HTTP/1.1 501 Not Implemented\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "examplebucket/a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_UnsignedTrailerNamingCrc64Nvme_Is501NotImplemented()
    {
        var harness = Harness();

        var response = await harness.SendRawAsync(TrailerPut("/examplebucket/a.txt", "x-amz-checksum-crc64nvme", "x-amz-checksum-crc64nvme:AAAAAAAAAAA="));

        StringAssert.StartsWith(response, "HTTP/1.1 501 Not Implemented\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithTwoChecksums_Is400InvalidRequest()
    {
        var harness = Harness();

        var response = await harness.SendWithBodyAsync("PUT", "/examplebucket/a.txt", "hello world", $"x-amz-checksum-crc32: {HelloWorldCrc32}", $"x-amz-checksum-sha1: {HelloWorldSha1}");

        StringAssert.Contains(response, "<Code>InvalidRequest</Code><Message>Expecting a single x-amz-checksum- header. Multiple checksum Types are not allowed.</Message>");
    }

    [TestMethod]
    [DataRow("x-amz-checksum-crc32: not base64", DisplayName = "not base64")]
    [DataRow("x-amz-checksum-crc32: " + HelloWorldSha1, DisplayName = "another algorithm's length")]
    [DataRow("x-amz-trailer: x-amz-checksum-crc32", DisplayName = "a trailer on a plain body")]
    public async Task ServeAsync_PutObjectWithAnUnreadableChecksum_Is400InvalidRequestAndNothingRead(string field)
    {
        var harness = Harness();

        var response = await harness.SendWithBodyAsync("PUT", "/examplebucket/a.txt", "hello world", field);

        StringAssert.Contains(response, "<Code>InvalidRequest</Code><Message>Value for x-amz-checksum header is invalid.</Message>");
    }

    [TestMethod]
    public async Task ServeAsync_UnsignedTrailerNamingNoChecksum_Is400InvalidRequest()
    {
        var harness = Harness();

        var response = await harness.SendRawAsync(TrailerPut("/examplebucket/a.txt", "x-amz-meta-colour", "x-amz-meta-colour:blue"));

        StringAssert.Contains(response, "<Code>InvalidRequest</Code><Message>Value for x-amz-checksum header is invalid.</Message>");
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartWithAnUnsignedTrailer_IsStagedAndTheChecksumEchoed()
    {
        var harness = Harness();
        var uploadId = await S3MultipartRequests.CreateUploadIdAsync(harness, "/examplebucket/a.txt");

        var response = await harness.SendRawAsync(TrailerPut($"/examplebucket/a.txt?partNumber=1&uploadId={uploadId}", "x-amz-checksum-crc32", $"x-amz-checksum-crc32:{HelloWorldCrc32}"));

        StringAssert.Contains(response, $"\r\nETag: {Md5ETag(Encoding.UTF8.GetBytes("hello world"))}\r\nx-amz-checksum-crc32: {HelloWorldCrc32}\r\nContent-Length: 0\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartWithAnotherBodysChecksum_Is400BadDigest()
    {
        var harness = Harness();
        var uploadId = await S3MultipartRequests.CreateUploadIdAsync(harness, "/examplebucket/a.txt");

        var response = await harness.SendWithBodyAsync("PUT", $"/examplebucket/a.txt?partNumber=1&uploadId={uploadId}", "hello", $"x-amz-checksum-crc32: {HelloWorldCrc32}");

        StringAssert.Contains(response, "<Code>BadDigest</Code><Message>The CRC32 you specified did not match the calculated checksum.</Message>");
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartChunkedWithABadChunk_Is400IncompleteBody()
    {
        var harness = Harness();
        var uploadId = await S3MultipartRequests.CreateUploadIdAsync(harness, "/examplebucket/a.txt");

        var response = await harness.SendRawAsync(TrailerPut($"/examplebucket/a.txt?partNumber=1&uploadId={uploadId}", "x-amz-checksum-crc32", "no colon"));

        StringAssert.Contains(response, "<Code>IncompleteBody</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_CompleteMultipartUploadAwsChunked_Is501NotImplemented()
    {
        var harness = Harness();
        var uploadId = await S3MultipartRequests.CreateUploadIdAsync(harness, "/examplebucket/a.txt");

        var response = await harness.SendRawAsync(TrailerPut($"/examplebucket/a.txt?uploadId={uploadId}", "x-amz-checksum-crc32", "x").Replace("PUT ", "POST ", StringComparison.Ordinal));

        StringAssert.StartsWith(response, "HTTP/1.1 501 Not Implemented\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_CompleteMultipartUploadWithItsObjectsChecksumField_IsNotCheckedAgainstTheBody()
    {
        var harness = Harness();
        var uploadId = await S3MultipartRequests.CreateUploadIdAsync(harness, "/examplebucket/a.txt");
        await harness.SendWithBodyAsync("PUT", $"/examplebucket/a.txt?partNumber=1&uploadId={uploadId}", "hello");

        var response = await harness.SendWithBodyAsync(
            "POST", $"/examplebucket/a.txt?uploadId={uploadId}", S3MultipartRequests.CompletionBody((1, S3MultipartRequests.ETagOf("hello"))), "x-amz-checksum-crc32: AAAAAA==-1");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("gzip", "gzip")]
    [DataRow("aws-chunked", null)]
    [DataRow("aws-chunked,gzip", "gzip")]
    [DataRow("gzip , AWS-CHUNKED, br", "gzip, br")]
    public void StoredContentEncoding_AwsChunked_IsLeftOut(string? sent, string? stored)
    {
        Assert.AreEqual(stored, S3PutObject.StoredContentEncoding(sent));
    }

    [TestMethod]
    public void AnnouncedObjectLength_AwsChunkedWithAnUnreadableDecodedLength_IsUnknown()
    {
        Assert.IsNull(S3ObjectBodyReader.DecodedLengthOf([new("x-amz-decoded-content-length", "-1")]));
        Assert.AreEqual(5, S3ObjectBodyReader.DecodedLengthOf([new("x-amz-decoded-content-length", "5")]));
    }
}
