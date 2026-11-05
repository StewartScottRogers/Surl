using System.Text;
using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Manifest and blob pulls, whose bytes are written from the OCI Distribution Specification
/// v1.1.0's "Pulling manifests", "Pulling blobs" and "Checking if content exists in the registry"
/// (ADR-0115, decisions 3 and 5.2).
/// </summary>
[TestClass]
public sealed class OciPullAnswersTests
{
    private const string Manifest = "{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"config\":{},\"layers\":[]}";

    [TestMethod]
    public async Task ServeAsync_ManifestByTag_ReturnsItsBytesWithItsMediaTypeAndDigest()
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("GET", "/v2/library/alpine/manifests/latest", "Accept: application/vnd.oci.image.manifest.v1+json");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: {H.ManifestMediaType}\r\nDocker-Content-Digest: {H.Sha256Of(Manifest)}\r\n"
                + $"Content-Length: {Manifest.Length}\r\nConnection: close\r\n\r\n{Manifest}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ManifestByDigest_ReturnsItsBytesWithItsMediaTypeAndDigest()
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("GET", $"/v2/library/alpine/manifests/{H.Sha256Of(Manifest)}");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: {H.ManifestMediaType}\r\nDocker-Content-Digest: {H.Sha256Of(Manifest)}\r\n"
                + $"Content-Length: {Manifest.Length}\r\nConnection: close\r\n\r\n{Manifest}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadManifest_ReturnsTheSameHeadersAndNoBody()
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("HEAD", "/v2/library/alpine/manifests/latest");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: {H.ManifestMediaType}\r\nDocker-Content-Digest: {H.Sha256Of(Manifest)}\r\n"
                + $"Content-Length: {Manifest.Length}\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ManifestWhoseDescriptorHasParameters_SendsTheMediaTypeWithoutThem()
    {
        var harness = new H();
        harness.CreateRepository("app");
        harness.WriteBlob("app", Manifest);
        harness.WriteIndex("app", H.Descriptor(H.ManifestMediaType + "; charset=utf-8", Manifest, "v1"));

        var response = await harness.SendAsync("GET", "/v2/app/manifests/v1");

        StringAssert.Contains(response, $"\r\nContent-Type: {H.ManifestMediaType}\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ManifestAPlatformIndexNames_IsServedByDigest()
    {
        var harness = new H();
        harness.CreateRepository("app");
        harness.WriteBlob("app", Manifest);
        var platforms = $"{{\"schemaVersion\":2,\"manifests\":[{H.Descriptor(H.ManifestMediaType, Manifest)}]}}";
        harness.WriteBlob("app", platforms);
        harness.WriteIndex("app", H.Descriptor(H.IndexMediaType, platforms, "latest"));

        var index = await harness.SendAsync("GET", "/v2/app/manifests/latest");
        var platform = await harness.SendAsync("GET", $"/v2/app/manifests/{H.Sha256Of(Manifest)}");

        StringAssert.Contains(index, $"\r\nContent-Type: {H.IndexMediaType}\r\n");
        StringAssert.EndsWith(platform, "\r\n\r\n" + Manifest);
    }

    [TestMethod]
    [DataRow("/v2/library/alpine/manifests/other")]
    [DataRow("/v2/library/alpine/manifests/.bad-tag")]
    [DataRow("/v2/library/alpine/manifests/sha256:abc")]
    [DataRow("/v2/library/alpine/manifests/sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task ServeAsync_NoManifestAtTheReference_Returns404ManifestUnknown(string target)
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("GET", target);

        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadOfAnUnknownManifest_ReturnsTheStatusAndNoBody()
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("HEAD", "/v2/library/alpine/manifests/other");

        var body = H.ErrorBody("MANIFEST_UNKNOWN", "manifest unknown");
        Assert.AreEqual($"HTTP/1.1 404 Not Found\r\n{H.CommonFields}Content-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n", response);
    }

    [TestMethod]
    public async Task ServeAsync_TaggedManifestWhoseFileIsMissing_Returns404ManifestUnknown()
    {
        var harness = new H();
        harness.CreateRepository("app");
        harness.WriteIndex("app", H.Descriptor(H.ManifestMediaType, Manifest, "latest"));

        var response = await harness.SendAsync("GET", "/v2/app/manifests/latest");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), response);
    }

    [TestMethod]
    public async Task ServeAsync_TaggedManifestWhoseFileDoesNotHashToItsDigest_Returns404ManifestUnknownAndNotes()
    {
        var harness = new H();
        harness.CreateRepository("app");
        harness.WriteFile($"app/blobs/sha256/{H.Sha256Of(Manifest)[7..]}", "tampered");
        harness.WriteIndex("app", H.Descriptor(H.ManifestMediaType, Manifest, "latest"));

        var response = await harness.SendAsync("GET", "/v2/app/manifests/latest");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), response);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("does not hash to its digest", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_TaggedManifestPast4MiB_Returns404ManifestUnknown()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var large = new string(' ', (int)OciRepository.MaxManifestBytes + 1);
        harness.WriteBlob("app", large);
        harness.WriteIndex("app", H.Descriptor(H.ManifestMediaType, large, "latest"));

        var response = await harness.SendAsync("GET", "/v2/app/manifests/latest");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), response);
    }

    [TestMethod]
    public async Task ServeAsync_TaggedDescriptorWithAnUnreadableDigest_Returns404ManifestUnknown()
    {
        var harness = new H();
        harness.CreateRepository("app");
        harness.WriteIndex("app", $"{{\"mediaType\":\"{H.ManifestMediaType}\",\"digest\":\"md5:x\",\"size\":1,\"annotations\":{{\"org.opencontainers.image.ref.name\":\"latest\"}}}}");

        var response = await harness.SendAsync("GET", "/v2/app/manifests/latest");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), response);
    }

    [TestMethod]
    public async Task ServeAsync_RepositoryWithoutIndex_HasNoManifests()
    {
        var harness = new H();
        harness.CreateRepository("app");

        var response = await harness.SendAsync("GET", "/v2/app/manifests/latest");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), response);
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("[]")]
    public async Task ServeAsync_IndexThatIsNotAnImageIndex_IsReadAsEmptyAndNoted(string index)
    {
        var harness = new H();
        harness.CreateRepository("app");
        harness.WriteFile("app/index.json", index);

        var response = await harness.SendAsync("GET", "/v2/app/manifests/latest");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), response);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("is not an image index", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_IndexPast16MiB_IsReadAsEmptyAndNoted()
    {
        var harness = new H();
        harness.CreateRepository("app");
        harness.WriteFile("app/index.json", new byte[OciRepository.MaxIndexBytes + 1]);

        var response = await harness.SendAsync("GET", "/v2/app/manifests/latest");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), response);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("is not an image index", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_BlobByDigest_ReturnsItsBytes()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", "layer bytes");

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: application/octet-stream\r\nDocker-Content-Digest: {digest}\r\n"
                + "Accept-Ranges: bytes\r\nContent-Length: 11\r\nConnection: close\r\n\r\nlayer bytes",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadBlob_ReturnsTheSameHeadersAndNoBody()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", "layer bytes");

        var response = await harness.SendAsync("HEAD", $"/v2/app/blobs/{digest}");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: application/octet-stream\r\nDocker-Content-Digest: {digest}\r\n"
                + "Accept-Ranges: bytes\r\nContent-Length: 11\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_BlobWithOneRange_Returns206WithThatRange()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", "layer bytes");

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}", "Range: bytes=6-10");

        Assert.AreEqual(
            $"HTTP/1.1 206 Partial Content\r\n{H.CommonFields}Content-Type: application/octet-stream\r\nDocker-Content-Digest: {digest}\r\n"
                + "Accept-Ranges: bytes\r\nContent-Range: bytes 6-10/11\r\nContent-Length: 5\r\nConnection: close\r\n\r\nbytes",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_BlobWithAnUnsatisfiableRange_Returns416WithTheSize()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", "layer bytes");

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}", "Range: bytes=20-");

        Assert.AreEqual(
            $"HTTP/1.1 416 Range Not Satisfiable\r\n{H.CommonFields}Content-Range: bytes */11\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_EmptyBlob_Returns200WithNoBytes()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", string.Empty);

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        StringAssert.EndsWith(response, "Content-Length: 0\r\nConnection: close\r\n\r\n");
        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_Sha512Blob_ReturnsItsBytes()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var bytes = Encoding.UTF8.GetBytes("sha512 layer");
        var hex = Convert.ToHexStringLower(System.Security.Cryptography.SHA512.HashData(bytes));
        harness.WriteFile($"app/blobs/sha512/{hex}", bytes);

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/sha512:{hex}");

        StringAssert.EndsWith(response, "\r\n\r\nsha512 layer");
    }

    [TestMethod]
    public async Task ServeAsync_BlobLongerThanOneHashChunk_IsCheckedAndServed()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var bytes = new byte[(1024 * 1024) + 5];
        bytes[^1] = 7;
        var digest = harness.WriteBlob("app", bytes);

        var response = await harness.SendAsync("HEAD", $"/v2/app/blobs/{digest}");

        StringAssert.Contains(response, $"Content-Length: {bytes.Length}\r\n");
        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("sha256:ABC")]
    [DataRow("latest")]
    public async Task ServeAsync_NoBlobAtTheDigest_Returns404BlobUnknown(string reference)
    {
        var harness = new H();
        harness.CreateRepository("app");

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{reference}");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "BLOB_UNKNOWN", "blob unknown to registry"), response);
    }

    [TestMethod]
    public async Task ServeAsync_BlobWhoseFileDoesNotHashToItsDigest_Returns404BlobUnknown()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = H.Sha256Of("original");
        harness.WriteFile($"app/blobs/sha256/{digest[7..]}", "tampered");

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "BLOB_UNKNOWN", "blob unknown to registry"), response);
    }

    [TestMethod]
    public async Task ServeAsync_BlobsEndpointForAManifestDigest_Returns404BlobUnknown()
    {
        var harness = RepositoryWithLatest();

        var response = await harness.SendAsync("GET", $"/v2/library/alpine/blobs/{H.Sha256Of(Manifest)}");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "BLOB_UNKNOWN", "blob unknown to registry"), response);
    }

    [TestMethod]
    public async Task ServeAsync_RepositoryNamedWithABlobsSegment_ServesItsBlob()
    {
        var harness = new H();
        harness.CreateRepository("team/blobs/tools");
        var digest = harness.WriteBlob("team/blobs/tools", "tool");

        var response = await harness.SendAsync("GET", $"/v2/team/blobs/tools/blobs/{digest}");

        StringAssert.EndsWith(response, "\r\n\r\ntool");
    }

    private static H RepositoryWithLatest()
    {
        var harness = new H();
        harness.CreateRepository("library/alpine");
        harness.WriteBlob("library/alpine", Manifest);
        harness.WriteIndex("library/alpine", H.Descriptor(H.ManifestMediaType, Manifest, "latest"));

        return harness;
    }
}
