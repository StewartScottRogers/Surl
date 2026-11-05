using System.Text;
using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Manifest pushes, whose bytes are written from the OCI Distribution Specification v1.1.0's
/// "Pushing Manifests" and "Pushing Manifests with Subject" and the image specification's "Image
/// Manifest" and "Image Index" (ADR-0115, decisions 3 and 3.1).
/// </summary>
[TestClass]
public sealed class OciManifestPushTests
{
    private const string Config = "{}";
    private const string Layer = "layer bytes";

    [TestMethod]
    public async Task AnswerAsync_ManifestByTag_Returns201AndIsPulledByTagAndDigest()
    {
        var harness = RepositoryWithBlobs();
        var manifest = ImageManifest();

        var response = await PushAsync(harness, "v1", manifest);
        var byTag = await harness.SendAsync("GET", "/v2/app/manifests/v1");
        var byDigest = await harness.SendAsync("GET", $"/v2/app/manifests/{H.Sha256Of(manifest)}");

        Assert.AreEqual(
            $"HTTP/1.1 201 Created\r\n{H.CommonFields}Location: /v2/app/manifests/{H.Sha256Of(manifest)}\r\n"
                + $"Docker-Content-Digest: {H.Sha256Of(manifest)}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        StringAssert.EndsWith(byTag, $"\r\n\r\n{manifest}");
        StringAssert.EndsWith(byDigest, $"\r\n\r\n{manifest}");
    }

    [TestMethod]
    public async Task AnswerAsync_ManifestByTag_ListsItTaggedInIndexJson()
    {
        var harness = RepositoryWithBlobs();
        var manifest = ImageManifest();

        await PushAsync(harness, "v1", manifest);

        Assert.AreEqual(
            $"{{\"schemaVersion\":2,\"mediaType\":\"{H.IndexMediaType}\",\"manifests\":[{{\"mediaType\":\"{H.ManifestMediaType}\","
                + $"\"digest\":\"{H.Sha256Of(manifest)}\",\"size\":{manifest.Length},\"annotations\":{{\"org.opencontainers.image.ref.name\":\"v1\"}}}}]}}",
            Encoding.UTF8.GetString(harness.ReadFile("app/index.json")!));
    }

    [TestMethod]
    public async Task AnswerAsync_TagMovedToAnotherManifest_KeepsTheOldOneByDigest()
    {
        var harness = RepositoryWithBlobs();
        var first = ImageManifest();
        var second = ImageManifest(annotation: "second");
        await PushAsync(harness, "latest", first);

        await PushAsync(harness, "latest", second);
        var tag = await harness.SendAsync("GET", "/v2/app/manifests/latest");
        var old = await harness.SendAsync("GET", $"/v2/app/manifests/{H.Sha256Of(first)}");

        StringAssert.EndsWith(tag, second);
        StringAssert.EndsWith(old, first);
    }

    [TestMethod]
    public async Task AnswerAsync_ManifestByDigest_Returns201WithThatDigest()
    {
        var harness = RepositoryWithBlobs();
        var manifest = ImageManifest();

        var response = await PushAsync(harness, H.Sha256Of(manifest), manifest);
        var again = await PushAsync(harness, H.Sha256Of(manifest), manifest);
        var pulled = await harness.SendAsync("GET", $"/v2/app/manifests/{H.Sha256Of(manifest)}");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        StringAssert.StartsWith(again, "HTTP/1.1 201 Created\r\n");
        StringAssert.EndsWith(pulled, manifest);
    }

    [TestMethod]
    public async Task AnswerAsync_ManifestByAWrongDigest_Returns400DigestInvalid()
    {
        var harness = RepositoryWithBlobs();

        var response = await PushAsync(harness, H.Sha256Of("other"), ImageManifest());

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "DIGEST_INVALID", "provided digest did not match uploaded content"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_ManifestByAnUnreadableDigest_Returns400DigestInvalid()
    {
        var harness = RepositoryWithBlobs();

        var response = await PushAsync(harness, "sha256:nothex", ImageManifest());

        StringAssert.Contains(response, "DIGEST_INVALID");
    }

    [TestMethod]
    public async Task AnswerAsync_ManifestNamingMissingBlobs_Returns400ManifestBlobUnknownOncePerDigest()
    {
        var harness = new H(new Content.ContentExposureOptions { AllowUploads = true });
        harness.CreateRepository("app");

        var response = await PushAsync(harness, "v1", ImageManifest());

        var entry = "{\"code\":\"MANIFEST_BLOB_UNKNOWN\",\"message\":\"manifest references a manifest or blob unknown to registry\"}";
        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.EndsWith(response, $"{{\"errors\":[{entry},{entry}]}}");
    }

    [TestMethod]
    public async Task AnswerAsync_IndexNamingAMissingManifest_Returns400ManifestBlobUnknown()
    {
        var harness = RepositoryWithBlobs();
        var index = $"{{\"schemaVersion\":2,\"mediaType\":\"{H.IndexMediaType}\",\"manifests\":[{H.Descriptor(H.ManifestMediaType, ImageManifest())}]}}";

        var response = await PushAsync(harness, "v1", index, H.IndexMediaType);

        StringAssert.Contains(response, "MANIFEST_BLOB_UNKNOWN");
    }

    [TestMethod]
    public async Task AnswerAsync_IndexOfPushedManifests_Returns201()
    {
        var harness = RepositoryWithBlobs();
        var manifest = ImageManifest();
        await PushAsync(harness, H.Sha256Of(manifest), manifest);
        var index = $"{{\"schemaVersion\":2,\"manifests\":[{H.Descriptor(H.ManifestMediaType, manifest)}]}}";

        var response = await PushAsync(harness, "multi", index, H.IndexMediaType);

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_LayerWithUrls_IsNotRequiredToBePresent()
    {
        var harness = RepositoryWithBlobs();
        var foreign = $"{{\"mediaType\":\"application/vnd.oci.image.layer.nondistributable.v1.tar\",\"digest\":\"{H.Sha256Of("elsewhere")}\",\"size\":9,\"urls\":[\"https://example.test/l\"]}}";
        var manifest = ImageManifest(extraLayer: foreign);

        var response = await PushAsync(harness, "v1", manifest);

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_ManifestWithASubject_AnsweredWithOciSubjectAndListedByTheReferrersApi()
    {
        var harness = RepositoryWithBlobs();
        var subject = ImageManifest();
        await PushAsync(harness, "v1", subject);
        var referrer = ImageManifest(subject: H.Descriptor(H.ManifestMediaType, subject), artifactType: "application/vnd.example.sbom");

        var response = await PushAsync(harness, H.Sha256Of(referrer), referrer);
        var referrers = await harness.SendAsync("GET", $"/v2/app/referrers/{H.Sha256Of(subject)}");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        StringAssert.Contains(response, $"\r\nOCI-Subject: {H.Sha256Of(subject)}\r\n");
        StringAssert.Contains(referrers, $"\"digest\":\"{H.Sha256Of(referrer)}\"");
        StringAssert.Contains(referrers, "\"artifactType\":\"application/vnd.example.sbom\"");
    }

    [TestMethod]
    public async Task AnswerAsync_ManifestWithASubjectThatDoesNotExist_IsAccepted()
    {
        var harness = RepositoryWithBlobs();
        var referrer = ImageManifest(subject: H.Descriptor(H.ManifestMediaType, "absent"));

        var response = await PushAsync(harness, "sbom", referrer);

        StringAssert.Contains(response, $"\r\nOCI-Subject: {H.Sha256Of("absent")}\r\n");
    }

    [TestMethod]
    [DataRow("application/json")]
    [DataRow("application/vnd.docker.distribution.manifest.v1+json")]
    public async Task AnswerAsync_ContentTypeNotAManifest_Returns400ManifestInvalid(string contentType)
    {
        var harness = RepositoryWithBlobs();

        var response = await PushAsync(harness, "v1", ImageManifest(), contentType);

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "MANIFEST_INVALID", "manifest invalid"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_NoContentType_Returns400ManifestInvalid()
    {
        var harness = RepositoryWithBlobs();

        var response = await harness.SendWithBodyAsync("PUT", "/v2/app/manifests/v1", ImageManifest());

        StringAssert.Contains(response, "MANIFEST_INVALID");
    }

    [TestMethod]
    public async Task AnswerAsync_ContentTypeWithParameters_IsAccepted()
    {
        var harness = RepositoryWithBlobs();

        var response = await PushAsync(harness, "v1", ImageManifest(), H.ManifestMediaType + "; charset=utf-8");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{\"schemaVersion\":1}")]
    [DataRow("{\"schemaVersion\":\"2\"}")]
    [DataRow("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\"}")]
    [DataRow("{\"schemaVersion\":2,\"mediaType\":7}")]
    [DataRow("{\"schemaVersion\":2,\"layers\":[]}")]
    [DataRow("{\"schemaVersion\":2,\"config\":{\"mediaType\":\"a\",\"digest\":\"sha256:00\",\"size\":2},\"layers\":[]}")]
    [DataRow("{\"schemaVersion\":2,\"config\":{\"digest\":\"x\",\"size\":2},\"layers\":[]}")]
    [DataRow("{\"schemaVersion\":2,\"config\":{\"mediaType\":\"a\",\"digest\":\"x\",\"size\":-1},\"layers\":[]}")]
    [DataRow("{\"schemaVersion\":2,\"config\":{\"mediaType\":\"a\",\"digest\":\"x\",\"size\":\"2\"},\"layers\":[]}")]
    [DataRow("{\"schemaVersion\":2,\"config\":[],\"layers\":[]}")]
    [DataRow("{\"schemaVersion\":2,\"config\":{\"mediaType\":\"a\",\"size\":2},\"layers\":[]}")]
    [DataRow("{\"schemaVersion\":2,\"config\":CONFIG}")]
    [DataRow("{\"schemaVersion\":2,\"config\":CONFIG,\"layers\":{}}")]
    [DataRow("{\"schemaVersion\":2,\"config\":CONFIG,\"layers\":[1]}")]
    [DataRow("{\"schemaVersion\":2,\"config\":CONFIG,\"layers\":[],\"subject\":{}}")]
    public async Task AnswerAsync_BodyNotAnImageManifest_Returns400ManifestInvalid(string body)
    {
        var harness = RepositoryWithBlobs();

        var response = await PushAsync(harness, "v1", body.Replace("CONFIG", H.Descriptor("application/vnd.oci.image.config.v1+json", Config)));

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "MANIFEST_INVALID", "manifest invalid"), response);
    }

    [TestMethod]
    [DataRow("{\"schemaVersion\":2}")]
    [DataRow("{\"schemaVersion\":2,\"manifests\":[{}]}")]
    public async Task AnswerAsync_BodyNotAnImageIndex_Returns400ManifestInvalid(string body)
    {
        var harness = RepositoryWithBlobs();

        var response = await PushAsync(harness, "v1", body, H.IndexMediaType);

        StringAssert.Contains(response, "MANIFEST_INVALID");
    }

    [TestMethod]
    public async Task AnswerAsync_TagOffTheGrammar_Returns400ManifestInvalid()
    {
        var harness = RepositoryWithBlobs();

        var response = await PushAsync(harness, "-bad", ImageManifest());

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "MANIFEST_INVALID", "manifest invalid"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_AnnouncedPast4MiB_Returns413ManifestInvalid()
    {
        var harness = RepositoryWithBlobs();

        var response = await harness.SendAsync("PUT", "/v2/app/manifests/v1", $"Content-Type: {H.ManifestMediaType}", $"Content-Length: {OciRepository.MaxManifestBytes + 1}");

        Assert.AreEqual(H.ExpectedError("413 Content Too Large", "MANIFEST_INVALID", "manifest invalid"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_ChunkedPast4MiB_Returns413ManifestInvalid()
    {
        var harness = RepositoryWithBlobs();
        var size = OciRepository.MaxManifestBytes + 1;
        var body = new string(' ', (int)size);

        var response = await harness.SendRawAsync(
            $"PUT /v2/app/manifests/v1 HTTP/1.1\r\nHost: registry.test\r\nContent-Type: {H.ManifestMediaType}\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n{size:x}\r\n{body}\r\n0\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 413 Content Too Large\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_ChunkedBodyCutShort_Returns400ManifestInvalid()
    {
        var harness = RepositoryWithBlobs();

        var response = await harness.SendRawAsync(
            $"PUT /v2/app/manifests/v1 HTTP/1.1\r\nHost: registry.test\r\nContent-Type: {H.ManifestMediaType}\r\nTransfer-Encoding: chunked\r\n\r\n5\r\n{{");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "MANIFEST_INVALID");
    }

    [TestMethod]
    public async Task AnswerAsync_NoBody_Returns400ManifestInvalid()
    {
        var harness = RepositoryWithBlobs();

        var response = await harness.SendAsync("PUT", "/v2/app/manifests/v1", $"Content-Type: {H.ManifestMediaType}");

        StringAssert.Contains(response, "MANIFEST_INVALID");
    }

    [TestMethod]
    public async Task AnswerAsync_IndexWithASubject_AnsweredWithOciSubject()
    {
        var harness = RepositoryWithBlobs();
        var manifest = ImageManifest();
        await PushAsync(harness, H.Sha256Of(manifest), manifest);
        var index = $"{{\"schemaVersion\":2,\"manifests\":[{H.Descriptor(H.ManifestMediaType, manifest)}],\"subject\":{H.Descriptor(H.ManifestMediaType, manifest)}}}";

        var response = await PushAsync(harness, H.Sha256Of(index), index, H.IndexMediaType);

        StringAssert.Contains(response, $"\r\nOCI-Subject: {H.Sha256Of(manifest)}\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_DockerManifestListOfPushedManifests_Returns201()
    {
        var harness = RepositoryWithBlobs();
        var manifest = ImageManifest();
        await PushAsync(harness, H.Sha256Of(manifest), manifest);
        var list = $"{{\"schemaVersion\":2,\"manifests\":[{H.Descriptor(H.ManifestMediaType, manifest)}]}}";

        var response = await PushAsync(harness, "multi", list, OciDescriptor.DockerManifestListMediaType);

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_RepositoryInAReadOnlyShare_Returns403Denied()
    {
        var harness = H.WithReadOnlyRepository();
        harness.WriteBlob("shares/ro/app", Config);
        harness.WriteBlob("shares/ro/app", Layer);

        var response = await harness.SendWithBodyAsync("PUT", "/v2/ro/app/manifests/v1", ImageManifest(), $"Content-Type: {H.ManifestMediaType}");

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "requested access to the resource is denied"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_ChunkedBodyPastTheExchangeUploadLimit_Returns413ManifestInvalid()
    {
        var harness = RepositoryWithBlobs();
        harness.Limits = H.LimitsWithMaxUpload(4);

        var response = await harness.SendRawAsync(
            $"PUT /v2/app/manifests/v1 HTTP/1.1\r\nHost: registry.test\r\nContent-Type: {H.ManifestMediaType}\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\na\r\n0123456789\r\n0\r\n\r\n");

        Assert.AreEqual(H.ExpectedError("413 Content Too Large", "MANIFEST_INVALID", "manifest invalid"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_UploadsOff_Returns403Denied()
    {
        var harness = new H();
        harness.CreateRepository("app");

        var response = await PushAsync(harness, "v1", ImageManifest());

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "requested access to the resource is denied"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_NameInTheWay_Returns400NameInvalid()
    {
        var harness = H.AllowingUploads();
        harness.WriteFile("app", "a file");

        var response = await PushAsync(harness, "v1", ImageManifest());

        StringAssert.Contains(response, "NAME_INVALID");
    }

    [TestMethod]
    public async Task AnswerAsync_RepositoryFull_Returns403RepositoryIsFull()
    {
        var harness = RepositoryWithBlobs();
        var descriptors = Enumerable.Range(0, OciIndexFile.MaxDescriptors).Select(number => H.Descriptor(H.ManifestMediaType, $"m{number}", $"t{number}")).ToArray();
        harness.WriteIndex("app", descriptors);

        var response = await PushAsync(harness, "v1", ImageManifest());

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "repository is full"), response);
    }

    private static Task<string> PushAsync(H harness, string reference, string manifest, string contentType = H.ManifestMediaType) =>
        harness.SendWithBodyAsync("PUT", $"/v2/app/manifests/{reference}", manifest, $"Content-Type: {contentType}");

    private static H RepositoryWithBlobs()
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("app");
        harness.WriteBlob("app", Config);
        harness.WriteBlob("app", Layer);

        return harness;
    }

    private static string ImageManifest(string? subject = null, string? artifactType = null, string? annotation = null, string? extraLayer = null)
    {
        var builder = new StringBuilder($"{{\"schemaVersion\":2,\"mediaType\":\"{H.ManifestMediaType}\"");
        if (artifactType is not null)
        {
            builder.Append($",\"artifactType\":\"{artifactType}\"");
        }

        builder.Append($",\"config\":{H.Descriptor("application/vnd.oci.image.config.v1+json", Config)}");
        builder.Append($",\"layers\":[{H.Descriptor("application/vnd.oci.image.layer.v1.tar", Layer)}{(extraLayer is null ? string.Empty : "," + extraLayer)}]");
        if (subject is not null)
        {
            builder.Append($",\"subject\":{subject}");
        }

        if (annotation is not null)
        {
            builder.Append($",\"annotations\":{{\"note\":\"{annotation}\"}}");
        }

        return builder.Append('}').ToString();
    }
}
