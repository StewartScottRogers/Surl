using System.Text;
using Surl.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// <see cref="DigestCalculation"/> against RFC 7616 section 3.9.1's worked example, and its
/// SHA-512-256 form against BL-112's <see cref="Sha512Slash256"/> directly.
/// </summary>
[TestClass]
public sealed class DigestCalculationTests
{
    private const string ExampleRealm = "http-auth@example.org";
    private const string ExampleNonce = "7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v";
    private const string ExampleCnonce = "f2/wE4q74E6zIJEtWaHKaf5wv/H5QzzpXusqGemxURZJ";

    private static readonly DigestResponseInputs ExampleInputs =
        new("GET", "/dir/index.html", ExampleNonce, "00000001", ExampleCnonce, "auth");

    private static string ExampleResponse(DigestAlgorithm algorithm) =>
        DigestCalculation.ComputeResponse(
            algorithm,
            DigestCalculation.ComputeUserHash(algorithm, Encoding.UTF8, "Mufasa", ExampleRealm, "Circle of Life"),
            ExampleInputs);

    [TestMethod]
    [DataRow(DigestAlgorithm.Md5, "8ca523f5e9506fed4657c9700eebdbec")]
    [DataRow(DigestAlgorithm.Sha256, "753927fa0e85d155564e2e272a28d1802ca10daf4496794697cf8db5856cb6c1")]
    public void ComputeResponse_Rfc7616Example_IsTheRfcsResponse(DigestAlgorithm algorithm, string expected)
    {
        var response = ExampleResponse(algorithm);

        Assert.AreEqual(expected, response);
    }

    [TestMethod]
    public void ComputeResponse_Rfc7616ExampleUnderSha512Slash256_IsBuiltOnSha512Slash256()
    {
        static string Hash(string text) => Convert.ToHexStringLower(Sha512Slash256.HashData(Encoding.UTF8.GetBytes(text)));
        var expected = Hash(
            $"{Hash($"Mufasa:{ExampleRealm}:Circle of Life")}:{ExampleNonce}:00000001:{ExampleCnonce}:auth:{Hash("GET:/dir/index.html")}");

        var response = ExampleResponse(DigestAlgorithm.Sha512Slash256);

        Assert.AreEqual(expected, response);
    }

    [TestMethod]
    public void ComputeA1Hash_SessionAlgorithm_HashesTheUserHashNonceAndCnonce()
    {
        var sessionHash = DigestCalculation.ComputeA1Hash(
            new DigestAlgorithmName(DigestAlgorithm.Md5, true), "ab", "n", "c");

        Assert.AreEqual(DigestCalculation.HashHex(DigestAlgorithm.Md5, "ab:n:c"u8), sessionHash);
    }

    [TestMethod]
    public void ComputeA1Hash_PlainAlgorithm_IsTheUserHash()
    {
        var a1Hash = DigestCalculation.ComputeA1Hash(new DigestAlgorithmName(DigestAlgorithm.Sha256, false), "ab", "n", "c");

        Assert.AreEqual("ab", a1Hash);
    }
}
