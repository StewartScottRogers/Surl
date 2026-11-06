using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The referrers API, written from the OCI Distribution Specification v1.1.0's "Listing
/// Referrers" and "Enabling the Referrers API" (ADR-0115, decision 5.3).
/// </summary>
[TestClass]
public sealed class OciReferrersTests
{
    private const string Image = "{\"schemaVersion\":2,\"config\":{\"mediaType\":\"application/vnd.oci.image.config.v1+json\"},\"layers\":[]}";
    private const string SbomType = "application/spdx+json";
    private const string SignatureType = "application/vnd.dev.cosign.artifact.sig.v1+json";

    private static readonly string ImageDigest = H.Sha256Of(Image);

    private static readonly string Sbom =
        $"{{\"schemaVersion\":2,\"artifactType\":\"{SbomType}\",\"config\":{{\"mediaType\":\"application/vnd.oci.empty.v1+json\"}},\"layers\":[],"
        + $"\"subject\":{{\"mediaType\":\"{H.ManifestMediaType}\",\"digest\":\"{ImageDigest}\",\"size\":{Image.Length}}},\"annotations\":{{\"created\":\"2026-11-05\"}}}}";

    private static readonly string Signature =
        $"{{\"schemaVersion\":2,\"config\":{{\"mediaType\":\"{SignatureType}\"}},\"layers\":[],"
        + $"\"subject\":{{\"mediaType\":\"{H.ManifestMediaType}\",\"digest\":\"{ImageDigest}\",\"size\":{Image.Length}}}}}";

    private static readonly string Bare =
        $"{{\"schemaVersion\":2,\"subject\":{{\"digest\":\"{ImageDigest}\"}}}}";

    [TestMethod]
    public async Task AnswerAsync_ManifestsWithTheSubject_AreListedAsAnImageIndexOrderedByDigest()
    {
        var harness = Repository();

        var response = await harness.SendAsync("GET", $"/v2/app/referrers/{ImageDigest}");

        var body = Index(Ordered(SbomEntry(), SignatureEntry(), BareEntry()));
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: {H.IndexMediaType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_ArtifactTypeFilter_ListsOnlyThatTypeAndSaysTheFilterApplied()
    {
        var harness = Repository();

        var response = await harness.SendAsync("GET", $"/v2/app/referrers/{ImageDigest}?artifactType=application%2Fspdx%2Bjson");

        var body = Index(SbomEntry());
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: {H.IndexMediaType}\r\nOCI-Filters-Applied: artifactType\r\n"
                + $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_UnknownSubject_IsAnEmptyIndex()
    {
        var harness = Repository();

        var response = await harness.SendAsync("GET", $"/v2/app/referrers/{H.Sha256Of("nothing")}");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, Index());
    }

    [TestMethod]
    [DataRow("sha256:xyz")]
    [DataRow("latest")]
    public async Task AnswerAsync_InvalidDigest_Returns400DigestInvalid(string reference)
    {
        var harness = Repository();

        var response = await harness.SendAsync("GET", $"/v2/app/referrers/{reference}");

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "DIGEST_INVALID", "provided digest did not match uploaded content"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_UnknownRepository_Returns404NameUnknown()
    {
        var harness = new H();

        var response = await harness.SendAsync("GET", $"/v2/app/referrers/{ImageDigest}");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "NAME_UNKNOWN", "repository name not known to registry"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_IndexTaggedWithTheReferrersTagSchema_ContributesItsEntries()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var listed = $"{{\"mediaType\":\"{H.ManifestMediaType}\",\"digest\":\"{H.Sha256Of("elsewhere")}\",\"size\":9,\"artifactType\":\"{SbomType}\"}}";
        var fallback = $"{{\"schemaVersion\":2,\"mediaType\":\"{H.IndexMediaType}\",\"manifests\":[{listed}]}}";
        harness.WriteBlob("app", fallback);
        harness.WriteIndex(
            "app",
            H.Descriptor(H.IndexMediaType, fallback, ImageDigest.Replace(':', '-')),
            H.Descriptor(H.ManifestMediaType, "{\"schemaVersion\":2}", "not-written"));

        var response = await harness.SendAsync("GET", $"/v2/app/referrers/{ImageDigest}");

        StringAssert.EndsWith(response, Index(listed));
    }

    [TestMethod]
    public async Task AnswerAsync_ReferrersTagWhoseIndexFileIsMissing_ContributesNothing()
    {
        var harness = new H();
        harness.CreateRepository("app");
        harness.WriteIndex("app", H.Descriptor(H.IndexMediaType, "{}", ImageDigest.Replace(':', '-')));

        var response = await harness.SendAsync("GET", $"/v2/app/referrers/{ImageDigest}");

        StringAssert.EndsWith(response, Index());
    }

    [TestMethod]
    public async Task AnswerAsync_HeadRequest_Returns405WithGetAllowed()
    {
        var harness = Repository();

        var response = await harness.SendAsync("HEAD", $"/v2/app/referrers/{ImageDigest}");

        StringAssert.StartsWith(response, "HTTP/1.1 405 Method Not Allowed\r\n");
        StringAssert.Contains(response, "\r\nAllow: GET\r\n");
    }

    private static H Repository()
    {
        var harness = new H();
        harness.CreateRepository("app");
        foreach (var manifest in new[] { Image, Sbom, Signature, Bare })
        {
            harness.WriteBlob("app", manifest);
        }

        harness.WriteBlob("app", "not json");
        harness.WriteIndex(
            "app",
            H.Descriptor(H.ManifestMediaType, Image, "latest"),
            H.Descriptor(H.ManifestMediaType, Sbom),
            H.Descriptor(H.ManifestMediaType, Signature),
            H.Descriptor(H.ManifestMediaType, Bare),
            H.Descriptor(H.ManifestMediaType, "not json"),
            H.Descriptor(H.ManifestMediaType, "missing file"));

        return harness;
    }

    private static string SbomEntry() =>
        $"{{\"mediaType\":\"{H.ManifestMediaType}\",\"digest\":\"{H.Sha256Of(Sbom)}\",\"size\":{Sbom.Length},\"artifactType\":\"{SbomType}\",\"annotations\":{{\"created\":\"2026-11-05\"}}}}";

    private static string SignatureEntry() =>
        $"{{\"mediaType\":\"{H.ManifestMediaType}\",\"digest\":\"{H.Sha256Of(Signature)}\",\"size\":{Signature.Length},\"artifactType\":\"{SignatureType}\"}}";

    private static string BareEntry() =>
        $"{{\"mediaType\":\"{H.ManifestMediaType}\",\"digest\":\"{H.Sha256Of(Bare)}\",\"size\":{Bare.Length}}}";

    private static string[] Ordered(params string[] entries) =>
        [.. entries.OrderBy(entry => entry[entry.IndexOf("\"digest\"", StringComparison.Ordinal)..], StringComparer.Ordinal)];

    private static string Index(params string[] entries) =>
        $"{{\"schemaVersion\":2,\"mediaType\":\"{H.IndexMediaType}\",\"manifests\":[{string.Join(',', entries)}]}}";
}
