namespace Surl.Cryptography.Curve25519;

[TestClass]
public sealed class X25519Tests
{
    // RFC 7748 section 5.2, first X25519 test vector.
    [TestMethod]
    public void ScalarMultiply_Rfc7748Section52FirstVector_ReturnsTheRfcOutput() =>
        AssertScalarMultiply(
            "a546e36bf0527c9d3b16154b82465edd62144c0ac1fc5a18506a2244ba449ac4",
            "e6db6867583030db3594c1a424b15f7c726624ec26b3353b10a903a6d0ab1c4c",
            "c3da55379de9c6908e94ea4df28d084f32eccf03491c71f754b4075577a28552");

    // RFC 7748 section 5.2, second X25519 test vector. Its u-coordinate has the top bit
    // set, which section 5 says to mask.
    [TestMethod]
    public void ScalarMultiply_Rfc7748Section52SecondVector_ReturnsTheRfcOutput() =>
        AssertScalarMultiply(
            "4b66e9d4d1b4673c5ad22691957d6af5c11b6421e0ea01d42ca4169e7918ba0d",
            "e5210f12786811d3f4b7959d0538ae2c31dbe7106fc03c3efc4cd549c715a493",
            "95cbde9476e8907d7aade45cb4b873f88b595a68799fa152e6f8f7647aac7957");

    // RFC 7748 section 5.2, iterated X25519: "After one iteration".
    [TestMethod]
    public void ScalarMultiply_Rfc7748Section52IteratedOnce_ReturnsTheRfcOutput() =>
        Assert.AreEqual("422c8e7a6227d7bca1350b3e2bb7279f7897b87bb6854b783c60e80311ae3079", Iterate(1));

    // RFC 7748 section 5.2, iterated X25519: "After 1,000 iterations".
    [TestMethod]
    public void ScalarMultiply_Rfc7748Section52IteratedAThousandTimes_ReturnsTheRfcOutput() =>
        Assert.AreEqual("684cf59ba83309552800ef566f2f4d3c1c3887c49360e3875f2eb94d99532c51", Iterate(1000));

    // RFC 7748 section 6.1: Alice's private key and her public key X25519(a, 9).
    [TestMethod]
    public void ComputePublicKey_Rfc7748Section61Alice_ReturnsHerPublicKey() =>
        AssertPublicKey(
            "77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a",
            "8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a");

    // RFC 7748 section 6.1: Bob's private key and his public key X25519(b, 9).
    [TestMethod]
    public void ComputePublicKey_Rfc7748Section61Bob_ReturnsHisPublicKey() =>
        AssertPublicKey(
            "5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb",
            "de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f");

    // RFC 7748 section 6.1: both sides reach the shared secret K.
    [TestMethod]
    [DataRow(
        "77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a",
        "de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f",
        DisplayName = "Alice's private key with Bob's public key")]
    [DataRow(
        "5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb",
        "8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a",
        DisplayName = "Bob's private key with Alice's public key")]
    public void ScalarMultiply_Rfc7748Section61KeyPair_ReturnsTheSharedSecret(string privateKey, string peerPublicKey) =>
        AssertScalarMultiply(privateKey, peerPublicKey, "4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742");

    // RFC 7748 section 6.1 leaves the all-zero check to the caller (RFC 8731 section 3 makes
    // it the SSH layer's): the low-order point u = 0 yields zero, returned as computed.
    [TestMethod]
    public void ScalarMultiply_LowOrderPointZero_ReturnsTheAllZeroResultAsComputed()
    {
        byte[] result = new byte[X25519.KeySize];
        result.AsSpan().Fill(0xAA);

        X25519.ScalarMultiply(Convert.FromHexString("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a"), new byte[X25519.KeySize], result);

        CollectionAssert.AreEqual(new byte[X25519.KeySize], result);
    }

    [TestMethod]
    [DataRow(31, 32, 32, "scalar")]
    [DataRow(32, 33, 32, "uCoordinate")]
    [DataRow(32, 32, 0, "result")]
    public void ScalarMultiply_SpanOfTheWrongLength_ThrowsNamingIt(int scalarLength, int uLength, int resultLength, string parameterName)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => X25519.ScalarMultiply(new byte[scalarLength], new byte[uLength], new byte[resultLength]));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    public void ComputePublicKey_PublicKeySpanOfTheWrongLength_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => X25519.ComputePublicKey(new byte[X25519.KeySize], new byte[16]));

    private static void AssertScalarMultiply(string scalar, string uCoordinate, string expected)
    {
        byte[] result = new byte[X25519.KeySize];

        X25519.ScalarMultiply(Convert.FromHexString(scalar), Convert.FromHexString(uCoordinate), result);

        Assert.AreEqual(expected, Convert.ToHexStringLower(result));
    }

    private static void AssertPublicKey(string privateKey, string expected)
    {
        byte[] publicKey = new byte[X25519.KeySize];

        X25519.ComputePublicKey(Convert.FromHexString(privateKey), publicKey);

        Assert.AreEqual(expected, Convert.ToHexStringLower(publicKey));
    }

    /// <summary>
    /// RFC 7748 section 5.2's iteration: k and u start as 9 encoded in 32 bytes, and each
    /// step sets k to X25519(k, u) and u to the old k.
    /// </summary>
    private static string Iterate(int iterations)
    {
        byte[] k = new byte[X25519.KeySize];
        byte[] u = new byte[X25519.KeySize];
        byte[] result = new byte[X25519.KeySize];
        k[0] = 9;
        u[0] = 9;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            X25519.ScalarMultiply(k, u, result);
            k.CopyTo(u, 0);
            result.CopyTo(k, 0);
        }

        return Convert.ToHexStringLower(k);
    }
}
