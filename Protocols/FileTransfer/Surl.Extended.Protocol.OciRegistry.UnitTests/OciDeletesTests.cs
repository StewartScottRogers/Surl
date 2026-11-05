using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Deletes, whose bytes are written from the OCI Distribution Specification v1.1.0's "Content
/// Management" (ADR-0115, decision 5.4).
/// </summary>
[TestClass]
public sealed class OciDeletesTests
{
    private const string Manifest = "{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"config\":{},\"layers\":[]}";

    private static readonly string Accepted = $"HTTP/1.1 202 Accepted\r\n{H.CommonFields}Content-Length: 0\r\nConnection: close\r\n\r\n";

    [TestMethod]
    public async Task DeleteManifestAsync_ByDigest_Returns202AndRemovesItsFileAndDescriptors()
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("DELETE", $"/v2/app/manifests/{H.Sha256Of(Manifest)}");
        var pulled = await harness.SendAsync("GET", "/v2/app/manifests/latest");

        Assert.AreEqual(Accepted, response);
        StringAssert.StartsWith(pulled, "HTTP/1.1 404 Not Found\r\n");
        Assert.IsNull(harness.ReadFile($"app/blobs/sha256/{H.Sha256Of(Manifest)["sha256:".Length..]}"));
    }

    [TestMethod]
    public async Task DeleteManifestAsync_ByTag_RemovesTheTagAndKeepsTheManifestByDigest()
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("DELETE", "/v2/app/manifests/latest");
        var byTag = await harness.SendAsync("GET", "/v2/app/manifests/latest");
        var byDigest = await harness.SendAsync("GET", $"/v2/app/manifests/{H.Sha256Of(Manifest)}");

        Assert.AreEqual(Accepted, response);
        StringAssert.StartsWith(byTag, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.EndsWith(byDigest, Manifest);
    }

    [TestMethod]
    [DataRow("other")]
    [DataRow("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task DeleteManifestAsync_NotListed_Returns404ManifestUnknown(string reference)
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("DELETE", $"/v2/app/manifests/{reference}");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), response);
    }

    [TestMethod]
    [DataRow("sha256:zz", "DIGEST_INVALID")]
    [DataRow("-bad", "MANIFEST_INVALID")]
    public async Task DeleteManifestAsync_ReferenceUnreadable_Returns400(string reference, string code)
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("DELETE", $"/v2/app/manifests/{reference}");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, code);
    }

    [TestMethod]
    public async Task DeleteManifestAsync_NoRepository_Returns404NameUnknown()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("DELETE", "/v2/app/manifests/latest");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "NAME_UNKNOWN", "repository name not known to registry"), response);
    }

    [TestMethod]
    public async Task DeleteBlobAsync_Blob_Returns202AndRemovesItsFile()
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", "bytes");

        var response = await harness.SendAsync("DELETE", $"/v2/app/blobs/{digest}");
        var pulled = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        Assert.AreEqual(Accepted, response);
        StringAssert.StartsWith(pulled, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    [DataRow("sha256:zz")]
    [DataRow("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task DeleteBlobAsync_NoSuchBlob_Returns404BlobUnknown(string reference)
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("app");

        var response = await harness.SendAsync("DELETE", $"/v2/app/blobs/{reference}");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "BLOB_UNKNOWN", "blob unknown to registry"), response);
    }

    [TestMethod]
    public async Task DeleteBlobAsync_NoRepository_Returns404NameUnknown()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("DELETE", $"/v2/app/blobs/{H.Sha256Of("x")}");

        StringAssert.Contains(response, "NAME_UNKNOWN");
    }

    [TestMethod]
    public async Task DeleteBlobAsync_UploadsOff_Returns403Denied()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", "bytes");

        var response = await harness.SendAsync("DELETE", $"/v2/app/blobs/{digest}");

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "requested access to the resource is denied"), response);
    }

    [TestMethod]
    public async Task DeleteBlobAsync_RepositoryInAReadOnlyShare_Returns403Denied()
    {
        var harness = H.WithReadOnlyRepository();
        var digest = harness.WriteBlob("shares/ro/app", "bytes");

        var response = await harness.SendAsync("DELETE", $"/v2/ro/app/blobs/{digest}");

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "requested access to the resource is denied"), response);
    }

    [TestMethod]
    public async Task DeleteManifestAsync_RepositoryInAReadOnlyShare_Returns403Denied()
    {
        var harness = H.WithReadOnlyRepository();
        harness.WriteBlob("shares/ro/app", Manifest);
        harness.WriteIndex("shares/ro/app", H.Descriptor(H.ManifestMediaType, Manifest, "latest"));

        var response = await harness.SendAsync("DELETE", "/v2/ro/app/manifests/latest");

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "requested access to the resource is denied"), response);
    }

    private static H RepositoryWithLatest()
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("app");
        harness.WriteBlob("app", Manifest);
        harness.WriteIndex("app", H.Descriptor(H.ManifestMediaType, Manifest, "latest"));

        return harness;
    }
}
