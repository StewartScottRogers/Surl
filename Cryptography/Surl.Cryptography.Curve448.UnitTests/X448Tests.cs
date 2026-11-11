namespace Surl.Cryptography.Curve448;

[TestClass]
public sealed class X448Tests
{
    // RFC 7748 section 6.2: Alice's private key a.
    private const string AlicePrivateKey =
        "9a8f4925d1519f5775cf46b04b5800d4ee9ee8bae8bc5565d498c28dd9c9baf574a9419744897391006382a6f127ab1d9ac2d8c0a598726b";

    // RFC 7748 section 6.2: Alice's public key X448(a, 5).
    private const string AlicePublicKey =
        "9b08f7cc31b7e3e67d22d5aea121074a273bd2b83de09c63faa73d2c22c5d9bbc836647241d953d40c5b12da88120d53177f80e532c41fa0";

    // RFC 7748 section 6.2: Bob's private key b.
    private const string BobPrivateKey =
        "1c306a7ac2a0e2e0990b294470cba339e6453772b075811d8fad0d1d6927c120bb5ee8972b0d3e21374c9c921b09d1b0366f10b65173992d";

    // RFC 7748 section 6.2: Bob's public key X448(b, 5).
    private const string BobPublicKey =
        "3eb7a829b0cd20f5bcfc0b599b6feccf6da4627107bdb0d4f345b43027d8b972fc3e34fb4232a13ca706dcb57aec3dae07bdc1c67bf33609";

    // RFC 7748 section 6.2: their shared secret K.
    private const string SharedSecret =
        "07fff4181ac6cc95ec1c16a94a0f74d12da232ce40a77552281d282bb60c0b56fd2464c335543936521c24403085d59a449a5037514a879d";

    // RFC 7748 section 5.2, first X448 test vector.
    [TestMethod]
    public void ScalarMultiply_Rfc7748Section52FirstVector_ReturnsTheRfcOutput() =>
        AssertScalarMultiply(
            "3d262fddf9ec8e88495266fea19a34d28882acef045104d0d1aae121700a779c984c24f8cdd78fbff44943eba368f54b29259a4f1c600ad3",
            "06fce640fa3487bfda5f6cf2d5263f8aad88334cbd07437f020f08f9814dc031ddbdc38c19c6da2583fa5429db94ada18aa7a7fb4ef8a086",
            "ce3e4ff95a60dc6697da1db1d85e6afbdf79b50a2412d7546d5f239fe14fbaadeb445fc66a01b0779d98223961111e21766282f73dd96b6f");

    // RFC 7748 section 5.2, second X448 test vector.
    [TestMethod]
    public void ScalarMultiply_Rfc7748Section52SecondVector_ReturnsTheRfcOutput() =>
        AssertScalarMultiply(
            "203d494428b8399352665ddca42f9de8fef600908e0d461cb021f8c538345dd77c3e4806e25f46d3315c44e0a5b4371282dd2c8d5be3095f",
            "0fbcc2f993cd56d3305b0b7d9e55d4c1a8fb5dbb52f8e9a1e9b6201b165d015894e56c4d3570bee52fe205e28a78b91cdfbde71ce8d157db",
            "884a02576239ff7a2f2f63b2db6a9ff37047ac13568e1e30fe63c4a7ad1b3ee3a5700df34321d62077e63633c575c1c954514e99da7c179d");

    // RFC 7748 section 5.2, iterated X448: "After one iteration".
    [TestMethod]
    public void ScalarMultiply_Rfc7748Section52IteratedOnce_ReturnsTheRfcOutput() =>
        Assert.AreEqual(
            "3f482c8a9f19b01e6c46ee9711d9dc14fd4bf67af30765c2ae2b846a4d23a8cd0db897086239492caf350b51f833868b9bc2b3bca9cf4113",
            Iterate(1));

    // RFC 7748 section 5.2, iterated X448: "After 1,000 iterations".
    [TestMethod]
    public void ScalarMultiply_Rfc7748Section52IteratedAThousandTimes_ReturnsTheRfcOutput() =>
        Assert.AreEqual(
            "aa3b4749d55b9daf1e5b00288826c467274ce3ebbdd5c17b975e09d4af6c67cf10d087202db88286e2b79fceea3ec353ef54faa26e219f38",
            Iterate(1000));

    // RFC 7748 section 6.2: Alice's private key and her public key X448(a, 5).
    [TestMethod]
    public void ComputePublicKey_Rfc7748Section62Alice_ReturnsHerPublicKey() =>
        AssertPublicKey(AlicePrivateKey, AlicePublicKey);

    // RFC 7748 section 6.2: Bob's private key and his public key X448(b, 5).
    [TestMethod]
    public void ComputePublicKey_Rfc7748Section62Bob_ReturnsHisPublicKey() =>
        AssertPublicKey(BobPrivateKey, BobPublicKey);

    // RFC 7748 section 6.2: both sides reach the shared secret K.
    [TestMethod]
    [DataRow(AlicePrivateKey, BobPublicKey, DisplayName = "Alice's private key with Bob's public key")]
    [DataRow(BobPrivateKey, AlicePublicKey, DisplayName = "Bob's private key with Alice's public key")]
    public void ScalarMultiply_Rfc7748Section62KeyPair_ReturnsTheSharedSecret(string privateKey, string peerPublicKey) =>
        AssertScalarMultiply(privateKey, peerPublicKey, SharedSecret);

    // RFC 7748 section 5: X448 masks no bit of u and decodes it modulo p, so u = p + 5
    // (2^448 - 2^224 + 4) is the base point 5, and Alice's key gives her section 6.2 public key.
    [TestMethod]
    public void ScalarMultiply_UCoordinateAbovePrime_IsReducedModuloPrime()
    {
        byte[] basePointPlusPrime = new byte[X448.KeySize];
        basePointPlusPrime[0] = 0x04;
        basePointPlusPrime.AsSpan(28).Fill(0xFF);

        AssertScalarMultiply(AlicePrivateKey, Convert.ToHexStringLower(basePointPlusPrime), AlicePublicKey);
    }

    // RFC 7748 section 6.2 leaves the all-zero check to the caller: the low-order point
    // u = 0 yields zero, returned as computed.
    [TestMethod]
    public void ScalarMultiply_LowOrderPointZero_ReturnsTheAllZeroResultAsComputed()
    {
        byte[] result = new byte[X448.KeySize];
        result.AsSpan().Fill(0xAA);

        X448.ScalarMultiply(Convert.FromHexString(AlicePrivateKey), new byte[X448.KeySize], result);

        CollectionAssert.AreEqual(new byte[X448.KeySize], result);
    }

    [TestMethod]
    [DataRow(55, 56, 56, "scalar")]
    [DataRow(56, 57, 56, "uCoordinate")]
    [DataRow(56, 56, 0, "result")]
    public void ScalarMultiply_SpanOfTheWrongLength_ThrowsNamingIt(int scalarLength, int uLength, int resultLength, string parameterName)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => X448.ScalarMultiply(new byte[scalarLength], new byte[uLength], new byte[resultLength]));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    [DataRow(32, 56, "scalar")]
    [DataRow(56, 32, "result")]
    public void ComputePublicKey_SpanOfTheWrongLength_ThrowsNamingIt(int privateKeyLength, int publicKeyLength, string parameterName)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => X448.ComputePublicKey(new byte[privateKeyLength], new byte[publicKeyLength]));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    private static void AssertScalarMultiply(string scalar, string uCoordinate, string expected)
    {
        byte[] result = new byte[X448.KeySize];

        X448.ScalarMultiply(Convert.FromHexString(scalar), Convert.FromHexString(uCoordinate), result);

        Assert.AreEqual(expected, Convert.ToHexStringLower(result));
    }

    private static void AssertPublicKey(string privateKey, string expected)
    {
        byte[] publicKey = new byte[X448.KeySize];

        X448.ComputePublicKey(Convert.FromHexString(privateKey), publicKey);

        Assert.AreEqual(expected, Convert.ToHexStringLower(publicKey));
    }

    /// <summary>
    /// RFC 7748 section 5.2's iteration: k and u start as 5 encoded in 56 bytes, and each
    /// step sets k to X448(k, u) and u to the old k.
    /// </summary>
    private static string Iterate(int iterations)
    {
        byte[] k = new byte[X448.KeySize];
        byte[] u = new byte[X448.KeySize];
        byte[] result = new byte[X448.KeySize];
        k[0] = 5;
        u[0] = 5;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            X448.ScalarMultiply(k, u, result);
            k.CopyTo(u, 0);
            result.CopyTo(k, 0);
        }

        return Convert.ToHexStringLower(k);
    }
}
