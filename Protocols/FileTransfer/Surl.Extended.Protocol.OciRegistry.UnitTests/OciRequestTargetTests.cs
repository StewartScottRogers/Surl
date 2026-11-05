namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Reading a request target as the distribution specification's endpoints (Dist "Endpoints";
/// ADR-0115, decision 1).
/// </summary>
[TestClass]
public sealed class OciRequestTargetTests
{
    [TestMethod]
    [DataRow("/v2/", nameof(OciEndpoint.Base), "", "", "")]
    [DataRow("/v2/?x=1", nameof(OciEndpoint.Base), "", "", "x=1")]
    [DataRow("/v2/a/b/manifests/latest", nameof(OciEndpoint.Manifest), "a/b", "latest", "")]
    [DataRow("/v2/a/manifests/b/manifests/v1", nameof(OciEndpoint.Manifest), "a/manifests/b", "v1", "")]
    [DataRow("/v2/a/blobs/sha256:00", nameof(OciEndpoint.Blob), "a", "sha256:00", "")]
    [DataRow("/v2/a/referrers/sha256:00?artifactType=t", nameof(OciEndpoint.Referrers), "a", "sha256:00", "artifactType=t")]
    [DataRow("/v2/a/tags/list?n=1", nameof(OciEndpoint.TagsList), "a", "", "n=1")]
    [DataRow("/v2/a%2Fb/tags/list", nameof(OciEndpoint.TagsList), "a/b", "", "")]
    [DataRow("/v2/a/manifests/", nameof(OciEndpoint.None), "", "", "")]
    [DataRow("/v2/a/blobs/uploads/", nameof(OciEndpoint.Uploads), "a", "", "")]
    [DataRow("/v2/a/blobs/uploads/?digest=sha256:00", nameof(OciEndpoint.Uploads), "a", "", "digest=sha256:00")]
    [DataRow("/v2/a/b/blobs/uploads/123", nameof(OciEndpoint.UploadSession), "a/b", "123", "")]
    [DataRow("/v2/a/blobs/uploads/1/2", nameof(OciEndpoint.None), "", "", "")]
    [DataRow("/other", nameof(OciEndpoint.None), "", "", "")]
    public void Parse_Target_NamesItsEndpointAndParts(string target, string endpoint, string name, string reference, string query)
    {
        Assert.AreEqual(new OciRequestTarget(Enum.Parse<OciEndpoint>(endpoint), name, reference, query), OciRequestTarget.Parse(target));
    }
}
