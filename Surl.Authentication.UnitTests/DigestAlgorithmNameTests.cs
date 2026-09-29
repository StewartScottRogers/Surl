namespace Surl.Authentication;

/// <summary>
/// <see cref="DigestAlgorithmName"/>: the six <c>algorithm</c> values RFC 7616 section 3.3 names
/// for MD5, SHA-256 and SHA-512-256.
/// </summary>
[TestClass]
public sealed class DigestAlgorithmNameTests
{
    [TestMethod]
    [DataRow("MD5", DigestAlgorithm.Md5, false)]
    [DataRow("md5-SESS", DigestAlgorithm.Md5, true)]
    [DataRow("SHA-256", DigestAlgorithm.Sha256, false)]
    [DataRow("SHA-256-sess", DigestAlgorithm.Sha256, true)]
    [DataRow("SHA-512-256", DigestAlgorithm.Sha512Slash256, false)]
    [DataRow("SHA-512-256-sess", DigestAlgorithm.Sha512Slash256, true)]
    [DataRow(null, DigestAlgorithm.Md5, false, DisplayName = "absent is MD5")]
    public void Parse_KnownValue_ReadsTheHashAndSession(string? value, DigestAlgorithm algorithm, bool isSession)
    {
        var name = DigestAlgorithmName.Parse(value);

        Assert.AreEqual(new DigestAlgorithmName(algorithm, isSession), name);
    }

    [TestMethod]
    [DataRow("SHA-512")]
    [DataRow("-sess")]
    [DataRow("MD5-sess-sess")]
    [DataRow("")]
    public void Parse_OtherValue_IsNull(string value)
    {
        Assert.IsNull(DigestAlgorithmName.Parse(value));
    }

    [TestMethod]
    [DataRow(DigestAlgorithm.Md5, "MD5")]
    [DataRow(DigestAlgorithm.Sha256, "SHA-256")]
    [DataRow(DigestAlgorithm.Sha512Slash256, "SHA-512-256")]
    public void NameOf_Algorithm_IsTheChallengeName(DigestAlgorithm algorithm, string expected)
    {
        Assert.AreEqual(expected, DigestAlgorithmName.NameOf(algorithm));
    }
}
