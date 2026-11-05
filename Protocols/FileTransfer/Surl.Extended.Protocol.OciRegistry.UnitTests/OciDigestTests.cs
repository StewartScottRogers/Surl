namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Reading a digest (Image "Descriptors"; ADR-0115, decision 3).
/// </summary>
[TestClass]
public sealed class OciDigestTests
{
    private static readonly string Hex64 = new('a', 64);
    private static readonly string Hex128 = new('0', 128);

    [TestMethod]
    public void Parse_Sha256AndSha512_AreRead()
    {
        var sha256 = OciDigest.Parse("sha256:" + Hex64)!;
        var sha512 = OciDigest.Parse("sha512:" + Hex128)!;

        Assert.AreEqual("sha256", sha256.Algorithm);
        Assert.AreEqual(Hex64, sha256.Encoded);
        Assert.AreEqual("sha512:" + Hex128, sha512.ToString());
        Assert.AreEqual("/a/b/blobs/sha512/" + Hex128, sha512.BlobPathIn("a/b"));
    }

    [TestMethod]
    [DataRow("sha256")]
    [DataRow("md5:d41d8cd98f00b204e9800998ecf8427e")]
    [DataRow("sha256:abc")]
    [DataRow("sha512:aaaa")]
    public void Parse_NotARegisteredDigest_IsNull(string text)
    {
        Assert.IsNull(OciDigest.Parse(text));
    }

    [TestMethod]
    public void Parse_UpperCaseHex_IsNull()
    {
        Assert.IsNull(OciDigest.Parse("sha256:" + new string('A', 64)));
    }
}
