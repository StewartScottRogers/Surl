namespace Surl.Cryptography.MlKem;

/// <summary>
/// The hand-built SHA-3 and SHAKE functions against NIST's published values: the empty message
/// (FIPS 202's functions as listed in NIST's SHA-3 example files) and the 1600-bit message of
/// bytes 0xA3, from NIST "Cryptographic Standards and Guidelines: Examples with Intermediate
/// Values", https://csrc.nist.gov/projects/cryptographic-standards-and-guidelines/example-values
/// (SHA3-256_1600, SHA3-512_1600, SHAKE128_Msg1600, SHAKE256_Msg1600). The 1600-bit message is
/// longer than every rate, so it absorbs over a block boundary; the 400-byte SHAKE128 output
/// squeezes over three.
/// </summary>
[TestClass]
public sealed class Sha3Tests
{
    private static readonly byte[] Message1600 = Enumerable.Repeat((byte)0xA3, 200).ToArray();

    [TestMethod]
    public void Sha3_256_EmptyMessage_IsNistsValue()
    {
        Assert.AreEqual("a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a", Hex(Sha3.Sha3_256([])));
    }

    [TestMethod]
    public void Sha3_512_EmptyMessage_IsNistsValue()
    {
        Assert.AreEqual(
            "a69f73cca23a9ac5c8b567dc185a756e97c982164fe25859e0d1dcc1475c80a6"
                + "15b2123af1f5f94c11e3e9402c3ac558f500199d95b6d3e301758586281dcd26",
            Hex(Sha3.Sha3_512([])));
    }

    [TestMethod]
    public void Sha3_256_Message1600_IsNistsValue()
    {
        Assert.AreEqual("79f38adec5c20307a98ef76e8324afbfd46cfd81b22e3973c65fa1bd9de31787", Hex(Sha3.Sha3_256(Message1600)));
    }

    [TestMethod]
    public void Sha3_512_Message1600_IsNistsValue()
    {
        Assert.AreEqual(
            "e76dfad22084a8b1467fcf2ffa58361bec7628edf5f3fdc0e4805dc48caeeca8"
                + "1b7c13c30adf52a3659584739a2df46be589c51ca1a4a8416df6545a1ce8ba00",
            Hex(Sha3.Sha3_512(Message1600)));
    }

    [TestMethod]
    public void Shake256_Message1600_IsNistsValue()
    {
        Assert.AreEqual(
            "cd8a920ed141aa0407a22d59288652e9d9f1a7ee0c1e7c1ca699424da84a904d"
                + "2d700caae7396ece96604440577da4f3aa22aeb8857f961c4cd8e06f0ae6610b",
            Hex(Sha3.Shake256(Message1600, 64)));
    }

    [TestMethod]
    public void Shake128_Message1600SqueezedInPieces_IsNistsValue()
    {
        var sponge = Sha3.Shake128(Message1600);
        var output = new byte[400];
        sponge.Squeeze(output.AsSpan(0, 3));
        sponge.Squeeze(output.AsSpan(3, 200));
        sponge.Squeeze(output.AsSpan(203));

        Assert.AreEqual(
            "131ab8d2b594946b9c81333f9bb6e0ce75c3b93104fa3469d3917457385da037cf232ef7164a6d1eb448c8908186ad852d3f85a5cf28da1ab6fe3438171978467f1c05d58c7ef38c284c41f6c2221a76f12ab1c04082660250802294fb87180213fdef5b0ecb7df50ca1f8555be14d32e10f6edcde892c09424b29f597afc270c904556bfcb47a7d40778d390923642b3cbd0579e60908d5a000c1d08b98ef933f806445bf87f8b009ba9e94f7266122ed7ac24e5e266c42a82fa1bbefb7b8db0066e16a85e0493f07df4809aec084a593748ac3dde5a6d7aae1e8b6e5352b2d71efbb47d4caeed5e6d633805d2d323e6fd81b4684b93a2677d45e7421c2c6aea259b855a698fd7d13477a1fe53e5a4a6197dbec5ce95f505b520bcd9570c4a8265a7e01f89c0c002c59bfec6cd4a5c109258953ee5ee70cd577ee217af21fa70178f0946c9bf6ca8751793479f6b537737e40b6ed28511d8a2d7e73eb75f8daac912ff906e0ab955b083bac45a8e5e9b744c8506f37e9b4e749a184b30f43eb188d855f1b70d71ff3e50c537ac1b0f8",
            Hex(output));
    }

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
