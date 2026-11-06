using System.Security.Cryptography;
using System.Text;

namespace Surl.TlsSrp;

/// <summary>
/// The TLS 1.2 PRF against the published SHA-256 TLS 1.2 PRF test vector that Joseph Birr-Pixton
/// posted to the IETF TLS working group list ("TLS 1.2 PRF test vectors", 2009,
/// https://mailarchive.ietf.org/arch/msg/tls/fzVCzk-z3FShgGJ6DOXqM1ydxms/): 100 bytes, label
/// "test label"; and the master secret, extended master secret, key block and <c>verify_data</c>
/// against RFC 5246's and RFC 7627's formulas computed by hand in <see cref="UnitTestPrf"/>.
/// </summary>
[TestClass]
public sealed class TlsPseudorandomFunctionTests
{
    private static readonly byte[] VectorSecret = Convert.FromHexString("9BBE436BA940F017B17652849A71DB35");
    private static readonly byte[] VectorSeed = Convert.FromHexString("A0BA9F936CDA311827A6F796FFD5198C");

    private const string VectorOutput =
        "E3F229BA727BE17B8D122620557CD453C2AAB21D07C3D495329B52D4E61EDB5A6B301791E90D35C9C9A46B4E14BAF9AF"
        + "0FA022F7077DEF17ABFD3797C0564BAB4FBC91666E9DEF9B97FCE34F796789BAA48082D122EE42C5A72E5A5110FFF7"
        + "0187347B66";

    private static readonly byte[] PreMasterSecret = Enumerable.Range(1, 256).Select(value => (byte)value).ToArray();
    private static readonly byte[] ClientRandom = Enumerable.Repeat((byte)0xC1, 32).ToArray();
    private static readonly byte[] ServerRandom = Enumerable.Repeat((byte)0x5E, 32).ToArray();

    [TestMethod]
    public void Compute_PublishedSha256Vector_MatchesThePublishedOutput()
    {
        var output = TlsPseudorandomFunction.Compute(VectorSecret, "test label", VectorSeed, 100);

        Assert.AreEqual(VectorOutput, Convert.ToHexString(output));
    }

    [TestMethod]
    public void UnitTestPrf_PublishedSha256Vector_MatchesThePublishedOutput()
    {
        Assert.AreEqual(VectorOutput, Convert.ToHexString(UnitTestPrf.Prf(VectorSecret, "test label", VectorSeed, 100)));
    }

    [TestMethod]
    public void Compute_ZeroBytes_IsEmpty()
    {
        Assert.IsEmpty(TlsPseudorandomFunction.Compute(VectorSecret, "test label", VectorSeed, 0));
    }

    [TestMethod]
    public void Compute_NegativeLength_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => TlsPseudorandomFunction.Compute(VectorSecret, "test label", VectorSeed, -1));
    }

    [TestMethod]
    public void MasterSecret_IsRfc5246Section8Point1sFormula()
    {
        var expected = UnitTestPrf.Prf(PreMasterSecret, "master secret", [.. ClientRandom, .. ServerRandom], 48);

        var masterSecret = TlsPseudorandomFunction.MasterSecret(PreMasterSecret, ClientRandom, ServerRandom);

        CollectionAssert.AreEqual(expected, masterSecret);
    }

    [TestMethod]
    public void ExtendedMasterSecret_IsRfc7627Section4sFormula()
    {
        var sessionHash = SHA256.HashData(Encoding.ASCII.GetBytes("handshake messages"));
        var expected = UnitTestPrf.Prf(PreMasterSecret, "extended master secret", sessionHash, 48);

        var masterSecret = TlsPseudorandomFunction.ExtendedMasterSecret(PreMasterSecret, sessionHash);

        CollectionAssert.AreEqual(expected, masterSecret);
    }

    [TestMethod]
    public void KeyBlock_IsRfc5246Section6Point3sFormula_ServerRandomFirst()
    {
        var masterSecret = TlsPseudorandomFunction.MasterSecret(PreMasterSecret, ClientRandom, ServerRandom);
        var expected = UnitTestPrf.Prf(masterSecret, "key expansion", [.. ServerRandom, .. ClientRandom], 104);

        var keyBlock = TlsPseudorandomFunction.KeyBlock(masterSecret, ClientRandom, ServerRandom, 104);

        CollectionAssert.AreEqual(expected, keyBlock);
    }

    [TestMethod]
    public void FinishedVerifyData_IsTwelveBytesOfThePrfUnderEachSidesLabel()
    {
        var masterSecret = TlsPseudorandomFunction.MasterSecret(PreMasterSecret, ClientRandom, ServerRandom);
        var handshakeHash = SHA256.HashData(Encoding.ASCII.GetBytes("transcript"));

        CollectionAssert.AreEqual(
            UnitTestPrf.Prf(masterSecret, "client finished", handshakeHash, 12),
            TlsPseudorandomFunction.ClientFinishedVerifyData(masterSecret, handshakeHash));
        CollectionAssert.AreEqual(
            UnitTestPrf.Prf(masterSecret, "server finished", handshakeHash, 12),
            TlsPseudorandomFunction.ServerFinishedVerifyData(masterSecret, handshakeHash));
    }
}
