using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Cryptography.Ed25519;

/// <summary>
/// Pins <see cref="Ed25519" /> to RFC 8032 section 7.1's published vectors and to the
/// rejections section 5.1.7 requires: a changed message or signature, a non-canonical S
/// and a public key that does not decode. Every expected value is copied from RFC 8032
/// (ADR-0003).
/// </summary>
[TestClass]
public sealed class Ed25519Tests
{
    private const string Test1PrivateKey = "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60";
    private const string Test1PublicKey = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";
    private const string Test1Signature =
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    // RFC 8032 section 7.1, TEST 1024: a 1023-byte message.
    private const string Test1024Message =
        "08b8b2b733424243760fe426a4b54908632110a66c2f6591eabd3345e3e4eb98fa6e264bf09efe12ee50f8f54e9f77b1" +
        "e355f6c50544e23fb1433ddf73be84d879de7c0046dc4996d9e773f4bc9efe5738829adb26c81b37c93a1b270b20329d" +
        "658675fc6ea534e0810a4432826bf58c941efb65d57a338bbd2e26640f89ffbc1a858efcb8550ee3a5e1998bd177e93a" +
        "7363c344fe6b199ee5d02e82d522c4feba15452f80288a821a579116ec6dad2b3b310da903401aa62100ab5d1a36553e" +
        "06203b33890cc9b832f79ef80560ccb9a39ce767967ed628c6ad573cb116dbefefd75499da96bd68a8a97b928a8bbc10" +
        "3b6621fcde2beca1231d206be6cd9ec7aff6f6c94fcd7204ed3455c68c83f4a41da4af2b74ef5c53f1d8ac70bdcb7ed1" +
        "85ce81bd84359d44254d95629e9855a94a7c1958d1f8ada5d0532ed8a5aa3fb2d17ba70eb6248e594e1a2297acbbb39d" +
        "502f1a8c6eb6f1ce22b3de1a1f40cc24554119a831a9aad6079cad88425de6bde1a9187ebb6092cf67bf2b13fd65f270" +
        "88d78b7e883c8759d2c4f5c65adb7553878ad575f9fad878e80a0c9ba63bcbcc2732e69485bbc9c90bfbd62481d9089b" +
        "eccf80cfe2df16a2cf65bd92dd597b0707e0917af48bbb75fed413d238f5555a7a569d80c3414a8d0859dc65a46128ba" +
        "b27af87a71314f318c782b23ebfe808b82b0ce26401d2e22f04d83d1255dc51addd3b75a2b1ae0784504df543af8969b" +
        "e3ea7082ff7fc9888c144da2af58429ec96031dbcad3dad9af0dcbaaaf268cb8fcffead94f3c7ca495e056a9b47acdb7" +
        "51fb73e666c6c655ade8297297d07ad1ba5e43f1bca32301651339e22904cc8c42f58c30c04aafdb038dda0847dd988d" +
        "cda6f3bfd15c4b4c4525004aa06eeff8ca61783aacec57fb3d1f92b0fe2fd1a85f6724517b65e614ad6808d6f6ee34df" +
        "f7310fdc82aebfd904b01e1dc54b2927094b2db68d6f903b68401adebf5a7e08d78ff4ef5d63653a65040cf9bfd4aca7" +
        "984a74d37145986780fc0b16ac451649de6188a7dbdf191f64b5fc5e2ab47b57f7f7276cd419c17a3ca8e1b939ae49e4" +
        "88acba6b965610b5480109c8b17b80e1b7b750dfc7598d5d5011fd2dcc5600a32ef5b52a1ecc820e308aa342721aac09" +
        "43bf6686b64b2579376504ccc493d97e6aed3fb0f9cd71a43dd497f01f17c0e2cb3797aa2a2f256656168e6c496afc5f" +
        "b93246f6b1116398a346f1a641f3b041e989f7914f90cc2c7fff357876e506b50d334ba77c225bc307ba537152f3f161" +
        "0e4eafe595f6d9d90d11faa933a15ef1369546868a7f3a45a96768d40fd9d03412c091c6315cf4fde7cb68606937380d" +
        "b2eaaa707b4c4185c32eddcdd306705e4dc1ffc872eeee475a64dfac86aba41c0618983f8741c5ef68d3a101e8a3b8ca" +
        "c60c905c15fc910840b94c00a0b9d0";

    // The group order L = 2^252 + 27742317777372353535851937790883648493 (RFC 8032 section 5.1).
    private static readonly BigInteger Order = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");

    public static IEnumerable<object[]> Rfc8032Section71Vectors =>
    [
        // TEST 1
        ["1", Test1PrivateKey, Test1PublicKey, "", Test1Signature],

        // TEST 2
        [
            "2",
            "4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb",
            "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c",
            "72",
            "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00",
        ],

        // TEST 3
        [
            "3",
            "c5aa8df43f9f837bedb7442f31dcb7b166d38535076f094b85ce3a2e0b4458f7",
            "fc51cd8e6218a1a38da47ed00230f0580816ed13ba3303ac5deb911548908025",
            "af82",
            "6291d657deec24024827e69c3abe01a30ce548a284743a445e3680d7db5ac3ac18ff9b538d16f290ae67f760984dc6594a7c15e9716ed28dc027beceea1ec40a",
        ],

        // TEST 1024
        [
            "1024",
            "f5e5767cf153319517630f226876b86c8160cc583bc013744c6bf255f5cc0ee5",
            "278117fc144c72340f67d0f2316e8386ceffbf2b2428c9c51fef7c597f1d426e",
            Test1024Message,
            "0aab4c900501b3e24d7cdf4663326a3a87df5e4843b2cbdb67cbf6e460fec350aa5371b1508f9f4528ecea23c436d94b5e8fcd4f681e30a6ac00a9704a188a03",
        ],

        // TEST SHA(abc): the message is SHA-512("abc").
        [
            "SHA(abc)",
            "833fe62409237b9d62ec77587520911e9a759cec1d19755b7da901b96dca3d42",
            "ec172b93ad5e563bf4932c70e1245034c35467ef2efd4d64ebf819683467e2bf",
            Convert.ToHexStringLower(SHA512.HashData("abc"u8)),
            "dc2a4459e7369633a52b1bf277839a00201009a3efbf3ecb69bea2186c26b58909351fc9ac90b3ecfdfbc7c66431e0303dca179c138ac17ad9bef1177331a704",
        ],
    ];

    [TestMethod]
    [DynamicData(nameof(Rfc8032Section71Vectors))]
    public void ComputePublicKey_Rfc8032Section71Vector_GivesThePublishedPublicKey(
        string test,
        string privateKey,
        string publicKey,
        string message,
        string signature)
    {
        byte[] result = Ed25519.ComputePublicKey(Convert.FromHexString(privateKey));

        Assert.AreEqual(publicKey, Convert.ToHexStringLower(result), $"TEST {test}");
        Assert.IsNotNull(message);
        Assert.IsNotNull(signature);
    }

    [TestMethod]
    [DynamicData(nameof(Rfc8032Section71Vectors))]
    public void Sign_Rfc8032Section71Vector_GivesThePublishedSignature(
        string test,
        string privateKey,
        string publicKey,
        string message,
        string signature)
    {
        byte[] result = Ed25519.Sign(Convert.FromHexString(privateKey), Convert.FromHexString(message));

        Assert.AreEqual(signature, Convert.ToHexStringLower(result), $"TEST {test}");
        Assert.IsNotNull(publicKey);
    }

    [TestMethod]
    [DynamicData(nameof(Rfc8032Section71Vectors))]
    public void Verify_Rfc8032Section71Vector_IsTrue(
        string test,
        string privateKey,
        string publicKey,
        string message,
        string signature)
    {
        bool valid = Ed25519.Verify(Convert.FromHexString(publicKey), Convert.FromHexString(message), Convert.FromHexString(signature));

        Assert.IsTrue(valid, $"TEST {test}");
        Assert.IsNotNull(privateKey);
    }

    // RFC 8032 section 7.1, TEST 3 with each bit of its two-byte message flipped in turn.
    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(15)]
    public void Verify_MessageBitFlipped_IsFalse(int bit)
    {
        byte[] message = Convert.FromHexString("af82");
        message[bit / 8] ^= (byte)(1 << (bit % 8));

        bool valid = Ed25519.Verify(
            Convert.FromHexString("fc51cd8e6218a1a38da47ed00230f0580816ed13ba3303ac5deb911548908025"),
            message,
            Convert.FromHexString("6291d657deec24024827e69c3abe01a30ce548a284743a445e3680d7db5ac3ac18ff9b538d16f290ae67f760984dc6594a7c15e9716ed28dc027beceea1ec40a"));

        Assert.IsFalse(valid);
    }

    // RFC 8032 section 7.1, TEST 1 with one bit of R (bits 0 and 255) or of S (bits 256 and 400) flipped.
    [TestMethod]
    [DataRow(0)]
    [DataRow(255)]
    [DataRow(256)]
    [DataRow(400)]
    public void Verify_SignatureBitFlipped_IsFalse(int bit)
    {
        byte[] signature = Convert.FromHexString(Test1Signature);
        signature[bit / 8] ^= (byte)(1 << (bit % 8));

        bool valid = Ed25519.Verify(Convert.FromHexString(Test1PublicKey), [], signature);

        Assert.IsFalse(valid);
    }

    // RFC 8032 section 5.1.7: S must be below L. S + L names the same point [S]B, so
    // only the range check rejects it; L itself is the smallest out-of-range S.
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Verify_SNotBelowTheGroupOrder_IsFalse(bool addToTheValidS)
    {
        byte[] signature = Convert.FromHexString(Test1Signature);
        BigInteger s = new(signature.AsSpan(32), isUnsigned: true);
        BigInteger replacement = addToTheValidS ? s + Order : Order;
        signature.AsSpan(32).Clear();
        Assert.IsTrue(replacement.TryWriteBytes(signature.AsSpan(32), out _, isUnsigned: true));

        bool valid = Ed25519.Verify(Convert.FromHexString(Test1PublicKey), [], signature);

        Assert.IsFalse(valid);
    }

    // A public key that does not decode (RFC 8032 section 5.1.3): y = p (not canonical),
    // y = 1 with the sign bit set (x = 0 cannot be negative), and y = 2, for which
    // (y^2 - 1) / (d y^2 + 1) has no square root.
    [TestMethod]
    [DataRow("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [DataRow("0100000000000000000000000000000000000000000000000000000000000080")]
    [DataRow("0200000000000000000000000000000000000000000000000000000000000000")]
    public void Verify_UndecodablePublicKey_IsFalse(string publicKey)
    {
        bool valid = Ed25519.Verify(Convert.FromHexString(publicKey), [], Convert.FromHexString(Test1Signature));

        Assert.IsFalse(valid);
    }

    [TestMethod]
    public void Sign_RandomSeed_VerifiesUnderItsPublicKey()
    {
        byte[] seed = RandomNumberGenerator.GetBytes(Ed25519.SeedSize);
        byte[] message = "surl"u8.ToArray();

        byte[] publicKey = Ed25519.ComputePublicKey(seed);
        byte[] signature = Ed25519.Sign(seed, message);

        Assert.IsTrue(Ed25519.Verify(publicKey, message, signature));
    }

    [TestMethod]
    [DataRow(31)]
    [DataRow(33)]
    public void ComputePublicKey_WrongSeedLength_Throws(int seedLength)
    {
        Assert.ThrowsExactly<ArgumentException>(() => Ed25519.ComputePublicKey(new byte[seedLength]));
    }

    [TestMethod]
    [DataRow(31)]
    [DataRow(33)]
    public void Sign_WrongSeedLength_Throws(int seedLength)
    {
        Assert.ThrowsExactly<ArgumentException>(() => Ed25519.Sign(new byte[seedLength], []));
    }

    [TestMethod]
    [DataRow(31, 64)]
    [DataRow(32, 65)]
    public void Verify_WrongLength_Throws(int publicKeyLength, int signatureLength)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => Ed25519.Verify(new byte[publicKeyLength], [], new byte[signatureLength]));
    }
}
