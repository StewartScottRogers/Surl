using System.Security.Cryptography;
using System.Text;

namespace Surl.Dtls;

/// <summary>
/// The TLS 1.2 PRF against the published TLS 1.2 PRF test vectors that Joseph Birr-Pixton posted
/// to the IETF TLS working group list ("TLS 1.2 PRF test vectors", 2009,
/// https://mailarchive.ietf.org/arch/msg/tls/fzVCzk-z3FShgGJ6DOXqM1ydxms/), the SHA-256 vector of
/// 100 bytes and the SHA-384 vector of 148 bytes, label "test label".
/// </summary>
[TestClass]
public sealed class DtlsPseudorandomFunctionTests
{
    private static readonly byte[] Sha256Secret = Convert.FromHexString("9BBE436BA940F017B17652849A71DB35");
    private static readonly byte[] Sha256Seed = Convert.FromHexString("A0BA9F936CDA311827A6F796FFD5198C");

    private const string Sha256Output =
        "E3F229BA727BE17B8D122620557CD453C2AAB21D07C3D495329B52D4E61EDB5A6B301791E90D35C9C9A46B4E14BAF9AF"
        + "0FA022F7077DEF17ABFD3797C0564BAB4FBC91666E9DEF9B97FCE34F796789BAA48082D122EE42C5A72E5A5110FFF7"
        + "0187347B66";

    private const string Sha384Output =
        "7B0C18E9CED410ED1804F2CFA34A336A1C14DFFB4900BB5FD7942107E81C83CDE9CA0FAA60BE9FE34F82B1233C9146A0"
        + "E534CB400FED2700884F9DC236F80EDD8BFA961144C9E8D792ECA722A7B32FC3D416D473EBC2C5FD4ABFDAD05D9184"
        + "259B5BF8CD4D90FA0D31E2DEC479E4F1A26066F2EEA9A69236A3E52655C9E9AEE691C8F3A26854308D5EAA3BE85E09"
        + "90703D73E56F";

    [TestMethod]
    public void Compute_Sha256Vector_MatchesThePublishedOutput()
    {
        var output = DtlsPseudorandomFunction.Compute(HashAlgorithmName.SHA256, Sha256Secret, "test label", Sha256Seed, 100);

        Assert.AreEqual(Sha256Output, Convert.ToHexString(output));
    }

    [TestMethod]
    public void Compute_Sha384Vector_MatchesThePublishedOutput()
    {
        var output = DtlsPseudorandomFunction.Compute(
            HashAlgorithmName.SHA384,
            Convert.FromHexString("B80B733D6CEEFCDC71566EA48E5567DF"),
            "test label",
            Convert.FromHexString("CD665CF6A8447DD6FF8B27555EDB7465"),
            148);

        Assert.AreEqual(Sha384Output, Convert.ToHexString(output));
    }

    [TestMethod]
    public void Compute_ZeroBytes_IsEmpty()
    {
        Assert.IsEmpty(DtlsPseudorandomFunction.Compute(HashAlgorithmName.SHA256, Sha256Secret, "test label", Sha256Seed, 0));
    }

    [TestMethod]
    public void Compute_NegativeLength_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => DtlsPseudorandomFunction.Compute(HashAlgorithmName.SHA256, Sha256Secret, "test label", Sha256Seed, -1));
    }

    /// <summary>
    /// RFC 7627 section 4: <c>PRF(pre_master_secret, "extended master secret", session_hash)</c>, 48 bytes,
    /// over the SHA-256 vector's secret and seed. The expected bytes were computed independently of this
    /// library, by a PowerShell P_SHA256 over the BCL's <c>HMACSHA256</c> that reproduces the vector above.
    /// </summary>
    [TestMethod]
    public void ExtendedMasterSecret_VectorSecretAndSeed_IsThePrfUnderItsLabel()
    {
        var masterSecret = DtlsPseudorandomFunction.ExtendedMasterSecret(HashAlgorithmName.SHA256, Sha256Secret, Sha256Seed);

        Assert.AreEqual(
            "D6BD1A5F9631586673443B935C00392BE876AD7D6C5CA0C8E43FA9F2E78ADFF3DE4CBCF53C948144A4A19CAE1DBBB08F",
            Convert.ToHexString(masterSecret));
    }

    [TestMethod]
    public void MasterSecret_IsThePrfOverClientThenServerRandom()
    {
        byte[] clientRandom = [.. Enumerable.Repeat((byte)0x01, 32)];
        byte[] serverRandom = [.. Enumerable.Repeat((byte)0x02, 32)];

        var masterSecret = DtlsPseudorandomFunction.MasterSecret(HashAlgorithmName.SHA256, Sha256Secret, clientRandom, serverRandom);

        CollectionAssert.AreEqual(
            DtlsPseudorandomFunction.Compute(HashAlgorithmName.SHA256, Sha256Secret, "master secret", [.. clientRandom, .. serverRandom], 48),
            masterSecret);
    }

    [TestMethod]
    public void KeyBlock_IsThePrfOverServerThenClientRandom()
    {
        byte[] clientRandom = [.. Enumerable.Repeat((byte)0x01, 32)];
        byte[] serverRandom = [.. Enumerable.Repeat((byte)0x02, 32)];

        var keyBlock = DtlsPseudorandomFunction.KeyBlock(HashAlgorithmName.SHA256, Sha256Secret, clientRandom, serverRandom, 40);

        CollectionAssert.AreEqual(
            DtlsPseudorandomFunction.Compute(HashAlgorithmName.SHA256, Sha256Secret, "key expansion", [.. serverRandom, .. clientRandom], 40),
            keyBlock);
    }

    [TestMethod]
    public void FinishedVerifyData_IsTwelveBytesUnderEachSidesLabel()
    {
        var handshakeHash = SHA256.HashData(Encoding.ASCII.GetBytes("handshake"));

        var client = DtlsPseudorandomFunction.ClientFinishedVerifyData(HashAlgorithmName.SHA256, Sha256Secret, handshakeHash);
        var server = DtlsPseudorandomFunction.ServerFinishedVerifyData(HashAlgorithmName.SHA256, Sha256Secret, handshakeHash);

        CollectionAssert.AreEqual(
            DtlsPseudorandomFunction.Compute(HashAlgorithmName.SHA256, Sha256Secret, "client finished", handshakeHash, 12), client);
        CollectionAssert.AreEqual(
            DtlsPseudorandomFunction.Compute(HashAlgorithmName.SHA256, Sha256Secret, "server finished", handshakeHash, 12), server);
    }
}
