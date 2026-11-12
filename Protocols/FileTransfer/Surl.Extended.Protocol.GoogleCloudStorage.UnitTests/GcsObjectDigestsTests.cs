namespace Surl.Extended.Protocol.GoogleCloudStorage;

[TestClass]
public sealed class GcsObjectDigestsTests
{
    private const string HelloMd5Hex = "5d41402abc4b2a76b9719d911017c592";

    [TestMethod]
    public void Cached_CrcAndQuotedMd5ETag_BuildsTheDigestsWithoutAFile()
    {
        var digests = GcsObjectDigests.Cached("\"" + HelloMd5Hex + "\"", 0x9a71bb4c);

        Assert.IsNotNull(digests);
        Assert.AreEqual("9a71bb4c", Convert.ToHexStringLower(digests.Crc32c));
        Assert.AreEqual(HelloMd5Hex, Convert.ToHexStringLower(digests.Md5));
        Assert.AreEqual(0x9a71bb4cu, digests.Crc32cValue);
        Assert.AreEqual("\"" + HelloMd5Hex + "\"", digests.ETag);
    }

    [TestMethod]
    public void Cached_UppercaseHex_IsAccepted()
    {
        var digests = GcsObjectDigests.Cached("\"" + HelloMd5Hex.ToUpperInvariant() + "\"", 1);

        Assert.AreEqual(HelloMd5Hex, Convert.ToHexStringLower(digests!.Md5));
    }

    [TestMethod]
    public void Cached_NoCrc_IsNull()
    {
        Assert.IsNull(GcsObjectDigests.Cached("\"" + HelloMd5Hex + "\"", null));
    }

    [TestMethod]
    [DataRow("\"5d41402abc4b2a76b9719d911017c592-3\"", DisplayName = "multipart ETag")]
    [DataRow("\"5d41402abc4b2a76b9719d911017c59\"", DisplayName = "too short")]
    [DataRow("\"5d41402abc4b2a76b9719d911017c59z\"", DisplayName = "not hex")]
    [DataRow("x5d41402abc4b2a76b9719d911017c592\"", DisplayName = "no opening quote")]
    [DataRow("\"5d41402abc4b2a76b9719d911017c592x", DisplayName = "no closing quote")]
    [DataRow("", DisplayName = "empty")]
    public void Cached_ETagThatIsNotAQuotedMd5_IsNull(string eTag)
    {
        Assert.IsNull(GcsObjectDigests.Cached(eTag, 1));
    }
}
