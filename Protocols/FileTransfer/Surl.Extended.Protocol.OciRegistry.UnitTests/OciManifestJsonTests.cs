using System.Text;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Reading image indexes and manifests (Image "Image Index", "Image Manifest", "Descriptors").
/// </summary>
[TestClass]
public sealed class OciManifestJsonTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("\"text\"")]
    [DataRow("{\"a\":")]
    public void Read_NotAJsonObject_IsNull(string json)
    {
        Assert.IsNull(OciManifestJson.Read(Encoding.UTF8.GetBytes(json)));
    }

    [TestMethod]
    public void Read_DeeperThan64_IsNull()
    {
        var json = new string('[', 65) + new string(']', 65);

        Assert.IsNull(OciManifestJson.Read(Encoding.UTF8.GetBytes("{\"a\":" + json + "}")));
    }

    [TestMethod]
    public void Read_DescriptorsMissingAField_AreSkipped()
    {
        const string Json = "{\"manifests\":[1,"
            + "{\"digest\":\"sha256:a\",\"size\":1},"
            + "{\"mediaType\":\"m\",\"size\":1},"
            + "{\"mediaType\":\"m\",\"digest\":\"sha256:a\"},"
            + "{\"mediaType\":\"m\",\"digest\":\"sha256:a\",\"size\":\"1\"},"
            + "{\"mediaType\":\"m\",\"digest\":\"sha256:a\",\"size\":-1},"
            + "{\"mediaType\":\"m\",\"digest\":\"sha256:a\",\"size\":1.5},"
            + "{\"mediaType\":\"m\",\"digest\":\"sha256:b\",\"size\":2,\"annotations\":{\"n\":1,\"s\":\"v\"}}]}";

        var facts = OciManifestJson.Read(Encoding.UTF8.GetBytes(Json))!;

        Assert.HasCount(1, facts.Manifests);
        Assert.AreEqual("sha256:b", facts.Manifests[0].Digest);
        Assert.AreEqual(2, facts.Manifests[0].Size);
        CollectionAssert.AreEqual(new[] { new KeyValuePair<string, string>("s", "v") }, facts.Manifests[0].Annotations.ToArray());
    }

    [TestMethod]
    public void Read_ManifestsNotAnArray_IsNoManifests()
    {
        var facts = OciManifestJson.Read(Encoding.UTF8.GetBytes("{\"manifests\":{},\"config\":[],\"subject\":\"x\",\"artifactType\":1}"))!;

        Assert.IsEmpty(facts.Manifests);
        Assert.IsNull(facts.ConfigMediaType);
        Assert.IsNull(facts.SubjectDigest);
        Assert.IsNull(facts.ArtifactType);
    }

    [TestMethod]
    public void IsImageIndex_DockerManifestList_IsAnIndex()
    {
        var descriptor = new OciDescriptor(OciDescriptor.DockerManifestListMediaType, "sha256:a", 1, null, []);

        Assert.IsTrue(descriptor.IsImageIndex);
        Assert.IsNull(descriptor.Tag);
    }
}
