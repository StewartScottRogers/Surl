using System.Text;
using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Blob uploads, whose bytes are written from the OCI Distribution Specification v1.1.0's "Push"
/// section: "POST then PUT", "Single POST", "Pushing a blob in chunks" and "Mounting a blob from
/// another repository" (ADR-0115, decisions 3 and 5.4).
/// </summary>
[TestClass]
public sealed class OciBlobUploadsTests
{
    private const string Blob = "hello world";

    private static readonly string BlobDigest = H.Sha256Of(Blob);

    private static readonly string Session1 = H.SessionId(1);

    [TestMethod]
    public async Task OpenAsync_Post_Returns202WithTheSessionLocationAndCreatesTheRepository()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("POST", "/v2/library/app/blobs/uploads/", "Content-Length: 0");

        Assert.AreEqual(
            $"HTTP/1.1 202 Accepted\r\n{H.CommonFields}Location: /v2/library/app/blobs/uploads/{Session1}\r\nRange: 0-0\r\n"
                + $"Docker-Upload-UUID: {Session1}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual("{\"imageLayoutVersion\":\"1.0.0\"}", Encoding.UTF8.GetString(harness.ReadFile("library/app/oci-layout")!));
    }

    [TestMethod]
    public async Task AnswerSessionAsync_PostThenPut_StoresTheBlobReadableByDigest()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendWithBodyAsync("PUT", $"/v2/app/blobs/uploads/{Session1}?digest={BlobDigest}", Blob, "Content-Type: application/octet-stream");
        var pulled = await harness.SendAsync("GET", $"/v2/app/blobs/{BlobDigest}");

        Assert.AreEqual(
            $"HTTP/1.1 201 Created\r\n{H.CommonFields}Location: /v2/app/blobs/{BlobDigest}\r\nDocker-Content-Digest: {BlobDigest}\r\n"
                + "Content-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        StringAssert.EndsWith(pulled, $"\r\n\r\n{Blob}");
    }

    [TestMethod]
    public async Task OpenAsync_SinglePostWithDigest_StoresTheBlobReadableByDigest()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendWithBodyAsync("POST", $"/v2/app/blobs/uploads/?digest={BlobDigest}", Blob);
        var pulled = await harness.SendAsync("GET", $"/v2/app/blobs/{BlobDigest}");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        StringAssert.Contains(response, $"\r\nLocation: /v2/app/blobs/{BlobDigest}\r\nDocker-Content-Digest: {BlobDigest}\r\n");
        StringAssert.EndsWith(pulled, $"\r\n\r\n{Blob}");
    }

    [TestMethod]
    public async Task OpenAsync_SinglePostOfASha512Digest_StoresTheBlobUnderSha512()
    {
        var harness = H.AllowingUploads();
        var hex = Convert.ToHexStringLower(System.Security.Cryptography.SHA512.HashData(Encoding.UTF8.GetBytes(Blob)));

        var response = await harness.SendWithBodyAsync("POST", $"/v2/app/blobs/uploads/?digest=sha512:{hex}", Blob);

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual(Blob, Encoding.UTF8.GetString(harness.ReadFile($"app/blobs/sha512/{hex}")!));
    }

    [TestMethod]
    public async Task AnswerSessionAsync_ChunksInOrder_EachAnsweredWithItsRangeAndTheBlobStored()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var first = await harness.SendWithBodyAsync("PATCH", $"/v2/app/blobs/uploads/{Session1}", "hello", "Content-Range: 0-4");
        var second = await harness.SendWithBodyAsync("PATCH", $"/v2/app/blobs/uploads/{Session1}", " worl", "Content-Range: 5-9");
        var status = await harness.SendAsync("GET", $"/v2/app/blobs/uploads/{Session1}");
        var last = await harness.SendWithBodyAsync("PUT", $"/v2/app/blobs/uploads/{Session1}?digest={BlobDigest}", "d", "Content-Range: 10-10");
        var pulled = await harness.SendAsync("GET", $"/v2/app/blobs/{BlobDigest}");

        Assert.AreEqual(
            $"HTTP/1.1 202 Accepted\r\n{H.CommonFields}Location: /v2/app/blobs/uploads/{Session1}\r\nRange: 0-4\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            first);
        StringAssert.Contains(second, "\r\nRange: 0-9\r\n");
        Assert.AreEqual(
            $"HTTP/1.1 204 No Content\r\n{H.CommonFields}Location: /v2/app/blobs/uploads/{Session1}\r\nRange: 0-9\r\nConnection: close\r\n\r\n",
            status);
        StringAssert.StartsWith(last, "HTTP/1.1 201 Created\r\n");
        StringAssert.EndsWith(pulled, $"\r\n\r\n{Blob}");
    }

    [TestMethod]
    public async Task AnswerSessionAsync_StreamedPatchWithoutContentRange_AppendsTheWholeBody()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var patch = await harness.SendWithBodyAsync("PATCH", $"/v2/app/blobs/uploads/{Session1}", Blob);
        var put = await harness.SendAsync("PUT", $"/v2/app/blobs/uploads/{Session1}?digest={BlobDigest}", "Content-Length: 0");

        StringAssert.Contains(patch, "\r\nRange: 0-10\r\n");
        StringAssert.StartsWith(put, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    public async Task AnswerSessionAsync_ChunkOutOfOrder_Returns416AndKeepsTheSession()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendWithBodyAsync("PATCH", $"/v2/app/blobs/uploads/{Session1}", "lo", "Content-Range: 3-4");
        var status = await harness.SendAsync("GET", $"/v2/app/blobs/uploads/{Session1}");

        Assert.AreEqual(H.ExpectedError("416 Range Not Satisfiable", "BLOB_UPLOAD_INVALID", "blob upload invalid"), response);
        StringAssert.StartsWith(status, "HTTP/1.1 204 No Content\r\n");
    }

    [TestMethod]
    [DataRow("0-9")]
    [DataRow("0-0")]
    public async Task AnswerSessionAsync_ChunkLengthNotItsContentRange_Returns400SizeInvalid(string contentRange)
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendWithBodyAsync("PATCH", $"/v2/app/blobs/uploads/{Session1}", "hello", $"Content-Range: {contentRange}");

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "SIZE_INVALID", "provided length did not match content length"), response);
    }

    [TestMethod]
    [DataRow("bytes")]
    [DataRow("x-4")]
    [DataRow("0-x")]
    [DataRow("4-0")]
    public async Task AnswerSessionAsync_ContentRangeUnreadable_Returns400BlobUploadInvalid(string contentRange)
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendWithBodyAsync("PATCH", $"/v2/app/blobs/uploads/{Session1}", "hello", $"Content-Range: {contentRange}");

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "BLOB_UPLOAD_INVALID", "blob upload invalid"), response);
    }

    [TestMethod]
    public async Task OpenAsync_SinglePostWithAWrongDigest_Returns400DigestInvalidAndStoresNothing()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendWithBodyAsync("POST", $"/v2/app/blobs/uploads/?digest={H.Sha256Of("other")}", Blob);

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "DIGEST_INVALID", "provided digest did not match uploaded content"), response);
        Assert.IsNull(harness.ReadFile($"app/blobs/sha256/{H.Sha256Of("other")["sha256:".Length..]}"));
    }

    [TestMethod]
    public async Task AnswerSessionAsync_PutWithAWrongDigest_Returns400DigestInvalidAndEndsTheSession()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendWithBodyAsync("PUT", $"/v2/app/blobs/uploads/{Session1}?digest={H.Sha256Of("other")}", Blob);
        var status = await harness.SendAsync("GET", $"/v2/app/blobs/uploads/{Session1}");

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "DIGEST_INVALID", "provided digest did not match uploaded content"), response);
        Assert.AreEqual(H.ExpectedError("404 Not Found", "BLOB_UPLOAD_UNKNOWN", "blob upload unknown to registry"), status);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("?digest=md5:00")]
    public async Task AnswerSessionAsync_PutWithoutAReadableDigest_Returns400DigestInvalid(string query)
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendAsync("PUT", $"/v2/app/blobs/uploads/{Session1}{query}", "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "DIGEST_INVALID", "provided digest did not match uploaded content"), response);
    }

    [TestMethod]
    public async Task AnswerSessionAsync_PutWithAChunkOutOfOrder_Returns416()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendWithBodyAsync("PUT", $"/v2/app/blobs/uploads/{Session1}?digest={BlobDigest}", "d", "Content-Range: 10-10");

        StringAssert.StartsWith(response, "HTTP/1.1 416 Range Not Satisfiable\r\n");
    }

    [TestMethod]
    public async Task OpenAsync_SinglePostWithAnUnreadableDigest_Returns400DigestInvalid()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendWithBodyAsync("POST", "/v2/app/blobs/uploads/?digest=sha256:XY", Blob);

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "DIGEST_INVALID", "provided digest did not match uploaded content"), response);
    }

    [TestMethod]
    public async Task OpenAsync_MountFromARepositoryHoldingTheBlob_Returns201AndCopiesIt()
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("source");
        harness.WriteBlob("source", Blob);

        var response = await harness.SendAsync("POST", $"/v2/target/blobs/uploads/?mount={BlobDigest}&from=source", "Content-Length: 0");
        var pulled = await harness.SendAsync("GET", $"/v2/target/blobs/{BlobDigest}");

        Assert.AreEqual(
            $"HTTP/1.1 201 Created\r\n{H.CommonFields}Location: /v2/target/blobs/{BlobDigest}\r\nDocker-Content-Digest: {BlobDigest}\r\n"
                + "Content-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        StringAssert.EndsWith(pulled, $"\r\n\r\n{Blob}");
    }

    [TestMethod]
    public async Task OpenAsync_MountOfAnEmptyBlob_Returns201()
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("source");
        var empty = harness.WriteBlob("source", string.Empty);

        var response = await harness.SendAsync("POST", $"/v2/target/blobs/uploads/?mount={empty}&from=source", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    [DataRow("&from=source")]
    [DataRow("&from=nowhere")]
    [DataRow("&from=Bad")]
    [DataRow("")]
    public async Task OpenAsync_MountNotPossible_Returns202WithANewSession(string from)
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("source");

        var response = await harness.SendAsync("POST", $"/v2/target/blobs/uploads/?mount={BlobDigest}{from}", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
        StringAssert.Contains(response, $"\r\nLocation: /v2/target/blobs/uploads/{Session1}\r\n");
    }

    [TestMethod]
    public async Task OpenAsync_MountWithAnUnreadableDigest_Returns400DigestInvalid()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("POST", "/v2/target/blobs/uploads/?mount=nonsense&from=source", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "DIGEST_INVALID");
    }

    [TestMethod]
    public async Task AnswerSessionAsync_SessionUntouchedForAnHour_Returns404BlobUploadUnknown()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");
        harness.TimeProvider.UtcNow += OciUploadSessions.IdleLifetime;

        var response = await harness.SendWithBodyAsync("PATCH", $"/v2/app/blobs/uploads/{Session1}", "hello", "Content-Range: 0-4");

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.EndsWith(response, H.ErrorBody("BLOB_UPLOAD_UNKNOWN", "blob upload unknown to registry"));
    }

    [TestMethod]
    public async Task AnswerSessionAsync_SessionTouchedWithinTheHour_StaysOpen()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");
        harness.TimeProvider.UtcNow += TimeSpan.FromMinutes(59);

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/uploads/{Session1}");

        StringAssert.StartsWith(response, "HTTP/1.1 204 No Content\r\n");
    }

    [TestMethod]
    public async Task AnswerSessionAsync_Delete_Returns204AndEndsTheSession()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendAsync("DELETE", $"/v2/app/blobs/uploads/{Session1}");
        var status = await harness.SendAsync("GET", $"/v2/app/blobs/uploads/{Session1}");

        Assert.AreEqual($"HTTP/1.1 204 No Content\r\n{H.CommonFields}Connection: close\r\n\r\n", response);
        StringAssert.StartsWith(status, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public async Task AnswerSessionAsync_SessionOfAnotherRepository_Returns404BlobUploadUnknown()
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("other");
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendAsync("GET", $"/v2/other/blobs/uploads/{Session1}");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "BLOB_UPLOAD_UNKNOWN", "blob upload unknown to registry"), response);
    }

    [TestMethod]
    public async Task AnswerSessionAsync_NoRepository_Returns404NameUnknown()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/uploads/{Session1}");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "NAME_UNKNOWN", "repository name not known to registry"), response);
    }

    [TestMethod]
    public async Task AnswerSessionAsync_ChunkPastMaxFilesize_Returns413SizeInvalidAndEndsTheSession()
    {
        var harness = new H(new Content.ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 40 });
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendWithBodyAsync("PATCH", $"/v2/app/blobs/uploads/{Session1}", new string('x', 50));
        var status = await harness.SendAsync("GET", $"/v2/app/blobs/uploads/{Session1}");

        Assert.AreEqual(H.ExpectedError("413 Content Too Large", "SIZE_INVALID", "provided length did not match content length"), response);
        StringAssert.StartsWith(status, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public async Task OpenAsync_MountPastMaxFilesize_Returns413SizeInvalid()
    {
        var harness = new H(new Content.ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 40 });
        harness.CreateRepository("source");
        var large = harness.WriteBlob("source", new string('x', 50));

        var response = await harness.SendAsync("POST", $"/v2/target/blobs/uploads/?mount={large}&from=source", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 413 Content Too Large\r\n");
    }

    [TestMethod]
    public async Task AnswerSessionAsync_ChunkedBodyCutShort_Returns400BlobUploadInvalid()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendRawAsync(
            $"PATCH /v2/app/blobs/uploads/{Session1} HTTP/1.1\r\nHost: registry.test\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhel");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "BLOB_UPLOAD_INVALID");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("?digest=DIGEST")]
    [DataRow("?mount=DIGEST&from=ro/app")]
    public async Task OpenAsync_RepositoryInAReadOnlyShare_Returns403Denied(string query)
    {
        var harness = H.WithReadOnlyRepository();
        harness.WriteBlob("shares/ro/app", Blob);

        var response = await harness.SendWithBodyAsync("POST", "/v2/ro/app/blobs/uploads/" + query.Replace("DIGEST", BlobDigest), Blob);

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "requested access to the resource is denied"), response);
    }

    [TestMethod]
    public async Task OpenAsync_BlobPathTakenByAFolder_Returns403Denied()
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("app");
        harness.FileSystem.CreateDirectory(Path.Join(Content.InMemoryContentFileSystem.RootPath, "app", "blobs"));
        harness.FileSystem.CreateDirectory(Path.Join(Content.InMemoryContentFileSystem.RootPath, "app", "blobs", "sha256"));
        harness.FileSystem.CreateDirectory(Path.Join(Content.InMemoryContentFileSystem.RootPath, "app", "blobs", "sha256", BlobDigest["sha256:".Length..]));

        var response = await harness.SendWithBodyAsync("POST", $"/v2/app/blobs/uploads/?digest={BlobDigest}", Blob);

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public async Task OpenAsync_PastTheOpenSessionBound_Returns429TooManyRequests()
    {
        var harness = H.AllowingUploads();
        for (var opened = 0; opened < OciUploadSessions.MaxOpen; opened++)
        {
            await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");
        }

        var response = await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("429 Too Many Requests", "TOOMANYREQUESTS", "too many requests"), response);
    }

    [TestMethod]
    public async Task OpenAsync_ExpiredSessionsFreeTheirPlaces_SoANewSessionOpens()
    {
        var harness = H.AllowingUploads();
        for (var opened = 0; opened < OciUploadSessions.MaxOpen; opened++)
        {
            await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");
        }

        harness.TimeProvider.UtcNow += OciUploadSessions.IdleLifetime;

        var response = await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
    }

    [TestMethod]
    public async Task AnswerSessionAsync_ChunkedBodyPastTheExchangeUploadLimit_Returns413SizeInvalid()
    {
        var harness = H.AllowingUploads();
        harness.Limits = H.LimitsWithMaxUpload(4);
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        var response = await harness.SendRawAsync(
            $"PATCH /v2/app/blobs/uploads/{Session1} HTTP/1.1\r\nHost: registry.test\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n5\r\nhello\r\n0\r\n\r\n");

        Assert.AreEqual(H.ExpectedError("413 Content Too Large", "SIZE_INVALID", "provided length did not match content length"), response);
    }

    [TestMethod]
    public async Task OpenAsync_UploadsOff_Returns403Denied()
    {
        var harness = new H();

        var response = await harness.SendWithBodyAsync("POST", $"/v2/app/blobs/uploads/?digest={BlobDigest}", Blob);

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "requested access to the resource is denied"), response);
        Assert.IsNull(harness.ReadFile("app/oci-layout"));
    }

    [TestMethod]
    public async Task OpenAsync_PushedBlob_IsAFileAtTheLayoutPathInTheContentStore()
    {
        var harness = H.AllowingUploads();

        await harness.SendWithBodyAsync("POST", $"/v2/library/app/blobs/uploads/?digest={BlobDigest}", Blob);

        Assert.AreEqual(Blob, Encoding.UTF8.GetString(harness.ReadFile($"library/app/blobs/sha256/{BlobDigest["sha256:".Length..]}")!));
        Assert.IsNull(harness.ReadFile($"library/app/blobs/oci-upload-{Session1}"));
    }
}
