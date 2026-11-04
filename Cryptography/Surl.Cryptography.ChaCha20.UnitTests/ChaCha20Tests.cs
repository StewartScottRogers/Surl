namespace Surl.Cryptography.ChaCha20;

/// <summary>
/// Pins <see cref="ChaCha20" /> to published vectors only (ADR-0003): RFC 8439 sections
/// 2.1.1, 2.2.1, 2.3.2 and 2.4.2 and Appendix A.1 and A.2 for the RFC form, and
/// draft-strombergson-chacha-test-vectors-01 section 7 for the original 64-bit-nonce form.
/// </summary>
[TestClass]
public sealed class ChaCha20Tests
{
    // RFC 8439 section 2.3.2 and 2.4.2: the key 00:01:02:...:1f.
    private const string SequentialKey = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";

    // RFC 8439 Appendix A.1 and A.2 keys and nonces.
    private const string ZeroKey = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string OneKey = "0000000000000000000000000000000000000000000000000000000000000001";
    private const string JabberwockyKey = "1c9240a5eb55d38af333888604f6b5f0473917c1402b80099dca5cbc207075c0";
    private const string ZeroNonce = "000000000000000000000000";
    private const string TwoNonce = "000000000000000000000002";
    private const string ZeroOriginalNonce = "0000000000000000";

    // RFC 8439 section 2.4.2: the "sunscreen" plaintext and its ciphertext.
    private const string SunscreenPlaintext =
        "4c616469657320616e642047656e746c656d656e206f662074686520636c617373206f66202739393a204966204920636f756c64206f6666657220796f75206f6e6c79206f6e652074697020666f7220746865206675747572652c2073756e73637265656e20776f756c642062652069742e";

    private const string SunscreenCiphertext =
        "6e2e359a2568f98041ba0728dd0d6981e97e7aec1d4360c20a27afccfd9fae0bf91b65c5524733ab8f593dabcd62b3571639d624e65152ab8f530c359f0861d807ca0dbf500d6a6156a38e088a22b65e52bc514d16ccf806818ce91ab77937365af90bbf74a35be6b40b8eedf2785e42874d";

    // RFC 8439 Appendix A.2, test vector #2.
    private const string IetfContributionPlaintext =
        "416e79207375626d697373696f6e20746f20746865204945544620696e74656e6465642062792074686520436f6e7472696275746f7220666f72207075626c69636174696f6e20617320616c6c206f722070617274206f6620616e204945544620496e7465726e65742d4472616674206f722052464320616e6420616e792073746174656d656e74206d6164652077697468696e2074686520636f6e74657874206f6620616e204945544620616374697669747920697320636f6e7369646572656420616e20224945544620436f6e747269627574696f6e222e20537563682073746174656d656e747320696e636c756465206f72616c2073746174656d656e747320696e20494554462073657373696f6e732c2061732077656c6c206173207772697474656e20616e6420656c656374726f6e696320636f6d6d756e69636174696f6e73206d61646520617420616e792074696d65206f7220706c6163652c207768696368206172652061646472657373656420746f";

    private const string IetfContributionCiphertext =
        "a3fbf07df3fa2fde4f376ca23e82737041605d9f4f4f57bd8cff2c1d4b7955ec2a97948bd3722915c8f3d337f7d370050e9e96d647b7c39f56e031ca5eb6250d4042e02785ececfa4b4bb5e8ead0440e20b6e8db09d881a7c6132f420e52795042bdfa7773d8a9051447b3291ce1411c680465552aa6c405b7764d5e87bea85ad00f8449ed8f72d0d662ab052691ca66424bc86d2df80ea41f43abf937d3259dc4b2d0dfb48a6c9139ddd7f76966e928e635553ba76c5c879d7b35d49eb2e62b0871cdac638939e25e8a1e0ef9d5280fa8ca328b351c3c765989cbcf3daa8b6ccc3aaf9f3979c92b3720fc88dc95ed84a1be059c6499b9fda236e7e818b04b0bc39c1e876b193bfe5569753f88128cc08aaa9b63d1a16f80ef2554d7189c411f5869ca52c5b83fa36ff216b9c1d30062bebcfd2dc5bce0911934fda79a86f6e698ced759c3ff9b6477338f3da4f9cd8514ea9982ccafb341b2384dd902f3d1ab7ac61dd29c6f21ba5b862f3730e37cfdc4fd806c22f221";

    // RFC 8439 Appendix A.2, test vector #3.
    private const string JabberwockyPlaintext =
        "2754776173206272696c6c69672c20616e642074686520736c6974687920746f7665730a446964206779726520616e642067696d626c6520696e2074686520776162653a0a416c6c206d696d737920776572652074686520626f726f676f7665732c0a416e6420746865206d6f6d65207261746873206f757467726162652e";

    private const string JabberwockyCiphertext =
        "62e6347f95ed87a45ffae7426f27a1df5fb69110044c0d73118effa95b01e5cf166d3df2d721caf9b21e5fb14c616871fd84c54f9d65b283196c7fe4f60553ebf39c6402c42234e32a356b3e764312a61a5532055716ead6962568f87d3f3f7704c6a8d1bcd1bf4d50d6154b6da731b187b58dfd728afa36757a797ac188d1";

    // RFC 8439 section 2.1.1.
    [TestMethod]
    public void QuarterRound_Rfc8439Section211Words_GivesThePublishedWords()
    {
        uint a = 0x11111111, b = 0x01020304, c = 0x9b8d6f43, d = 0x01234567;

        ChaCha20.QuarterRound(ref a, ref b, ref c, ref d);

        Assert.AreEqual(0xea2a92f4u, a);
        Assert.AreEqual(0xcb1cf8ceu, b);
        Assert.AreEqual(0x4581472eu, c);
        Assert.AreEqual(0x5881c4bbu, d);
    }

    // RFC 8439 section 2.2.1: QUARTERROUND(2, 7, 8, 13) on the sample state changes only
    // the four starred words.
    [TestMethod]
    public void QuarterRound_Rfc8439Section221State_ChangesOnlyTheFourNamedWords()
    {
        uint[] state =
        [
            0x879531e0, 0xc5ecf37d, 0x516461b1, 0xc9a62f8a,
            0x44c20ef3, 0x3390af7f, 0xd9fc690b, 0x2a5f714c,
            0x53372767, 0xb00a5631, 0x974c541a, 0x359e9963,
            0x5c971061, 0x3d631689, 0x2098d9d6, 0x91dbd320,
        ];
        uint[] expected =
        [
            0x879531e0, 0xc5ecf37d, 0xbdb886dc, 0xc9a62f8a,
            0x44c20ef3, 0x3390af7f, 0xd9fc690b, 0xcfacafd2,
            0xe46bea80, 0xb00a5631, 0x974c541a, 0x359e9963,
            0x5c971061, 0xccc07c79, 0x2098d9d6, 0x91dbd320,
        ];

        ChaCha20.QuarterRound(state, 2, 7, 8, 13);

        CollectionAssert.AreEqual(expected, state);
    }

    // RFC 8439 section 2.3.2 (the serialized block), then Appendix A.1 test vectors #1 to #5.
    [TestMethod]
    [DataRow(SequentialKey, "000000090000004a00000000", 1U,
        "10f1e7e4d13b5915500fdd1fa32071c4c7d1f4c733c068030422aa9ac3d46c4ed2826446079faa0914c2d705d98b02a2b5129cd1de164eb9cbd083e8a2503c4e")]
    [DataRow(ZeroKey, ZeroNonce, 0U,
        "76b8e0ada0f13d90405d6ae55386bd28bdd219b8a08ded1aa836efcc8b770dc7da41597c5157488d7724e03fb8d84a376a43b8f41518a11cc387b669b2ee6586")]
    [DataRow(ZeroKey, ZeroNonce, 1U,
        "9f07e7be5551387a98ba977c732d080dcb0f29a048e3656912c6533e32ee7aed29b721769ce64e43d57133b074d839d531ed1f28510afb45ace10a1f4b794d6f")]
    [DataRow(OneKey, ZeroNonce, 1U,
        "3aeb5224ecf849929b9d828db1ced4dd832025e8018b8160b82284f3c949aa5a8eca00bbb4a73bdad192b5c42f73f2fd4e273644c8b36125a64addeb006c13a0")]
    [DataRow("00ff000000000000000000000000000000000000000000000000000000000000", ZeroNonce, 2U,
        "72d54dfbf12ec44b362692df94137f328fea8da73990265ec1bbbea1ae9af0ca13b25aa26cb4a648cb9b9d1be65b2c0924a66c54d545ec1b7374f4872e99f096")]
    [DataRow(ZeroKey, TwoNonce, 0U,
        "c2c64d378cd536374ae204b9ef933fcd1a8b2288b3dfa49672ab765b54ee27c78a970e0e955c14f3a88e741b97c286f75f8fc299e8148362fa198a39531bed6d")]
    public void ComputeBlock_Rfc8439Vector_GivesThePublishedBlock(string key, string nonce, uint counter, string expected)
    {
        byte[] block = new byte[ChaCha20.BlockSize];

        ChaCha20.ComputeBlock(Convert.FromHexString(key), Convert.FromHexString(nonce), counter, block);

        Assert.AreEqual(expected, Convert.ToHexStringLower(block));
    }

    // RFC 8439 section 2.4.2, then Appendix A.2 test vectors #1 to #3.
    [TestMethod]
    [DataRow(SequentialKey, "000000000000004a00000000", 1U, SunscreenPlaintext, SunscreenCiphertext)]
    [DataRow(ZeroKey, ZeroNonce, 0U,
        "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
        "76b8e0ada0f13d90405d6ae55386bd28bdd219b8a08ded1aa836efcc8b770dc7da41597c5157488d7724e03fb8d84a376a43b8f41518a11cc387b669b2ee6586")]
    [DataRow(OneKey, TwoNonce, 1U, IetfContributionPlaintext, IetfContributionCiphertext)]
    [DataRow(JabberwockyKey, TwoNonce, 42U, JabberwockyPlaintext, JabberwockyCiphertext)]
    public void ApplyKeyStream_Rfc8439Vector_EncryptsAndDecryptsThePublishedText(
        string key,
        string nonce,
        uint initialCounter,
        string plaintext,
        string ciphertext)
    {
        byte[] keyBytes = Convert.FromHexString(key);
        byte[] nonceBytes = Convert.FromHexString(nonce);
        byte[] encrypted = new byte[plaintext.Length / 2];
        byte[] decrypted = new byte[encrypted.Length];

        ChaCha20.ApplyKeyStream(keyBytes, nonceBytes, initialCounter, Convert.FromHexString(plaintext), encrypted);
        ChaCha20.ApplyKeyStream(keyBytes, nonceBytes, initialCounter, encrypted, decrypted);

        Assert.AreEqual(ciphertext, Convert.ToHexStringLower(encrypted));
        Assert.AreEqual(plaintext, Convert.ToHexStringLower(decrypted));
    }

    // draft-strombergson-chacha-test-vectors-01 section 7, 256-bit key, 20 rounds:
    // TC7 "Sequence patterns in key and IV" and TC8 "All your base are belong to us!" /
    // "IETF2013". Each lists keystream block 0 and keystream block 1.
    [TestMethod]
    [DataRow("00112233445566778899aabbccddeeffffeeddccbbaa99887766554433221100", "0f1e2d3c4b5a6978",
        "9fadf409c00811d00431d67efbd88fba59218d5d6708b1d685863fabbb0e961eea480fd6fb532bfd494b2151015057423ab60a63fe4f55f7a212e2167ccab931",
        "fbfd29cf7bc1d279eddf25dd316bb8843d6edee0bd1ef121d12fa17cbc2c574cccab5e275167b08bd686f8a09df87ec3ffb35361b94ebfa13fec0e4889d18da5")]
    [DataRow("c46ec1b18ce8a878725a37e780dfb7351f68ed2e194c79fbc6aebee1a667975d", "1ada31d5cf688221",
        "f63a89b75c2271f9368816542ba52f06ed49241792302b00b5e8f80ae9a473afc25b218f519af0fdd406362e8d69de7f54c604a6e00f353f110f771bdca8ab92",
        "e5fbc34e60a1d9a9db17345b0a402736853bf910b060bdf1f897b6290f01d138ae2c4c90225ba9ea14d518f55929dea098ca7a6ccfe61227053c84e49a4a3332")]
    public void OriginalForm_StrombergsonVector_GivesThePublishedKeystream(string key, string nonce, string block0, string block1)
    {
        byte[] keyBytes = Convert.FromHexString(key);
        byte[] nonceBytes = Convert.FromHexString(nonce);
        byte[] first = new byte[ChaCha20.BlockSize];
        byte[] second = new byte[ChaCha20.BlockSize];
        byte[] keyStream = new byte[2 * ChaCha20.BlockSize];

        ChaCha20.ComputeOriginalBlock(keyBytes, nonceBytes, 0, first);
        ChaCha20.ComputeOriginalBlock(keyBytes, nonceBytes, 1, second);
        ChaCha20.ApplyOriginalKeyStream(keyBytes, nonceBytes, 0, new byte[keyStream.Length], keyStream);

        Assert.AreEqual(block0, Convert.ToHexStringLower(first));
        Assert.AreEqual(block1, Convert.ToHexStringLower(second));
        Assert.AreEqual(block0 + block1, Convert.ToHexStringLower(keyStream));
    }

    [TestMethod]
    public void ApplyKeyStream_DestinationIsTheSource_EncryptsInPlace()
    {
        byte[] buffer = Convert.FromHexString(SunscreenPlaintext);

        ChaCha20.ApplyKeyStream(Convert.FromHexString(SequentialKey), Convert.FromHexString("000000000000004a00000000"), 1, buffer, buffer);

        Assert.AreEqual(SunscreenCiphertext, Convert.ToHexStringLower(buffer));
    }

    [TestMethod]
    public void ApplyKeyStream_EmptySourceAtTheLargestCounter_WritesNothing() =>
        ChaCha20.ApplyKeyStream(Convert.FromHexString(ZeroKey), Convert.FromHexString(ZeroNonce), uint.MaxValue, [], []);

    // The original form puts the 64-bit counter in words 12 and 13 and the nonce in words
    // 14 and 15, so its block equals RFC 8439's block whose 32-bit counter is the low word
    // and whose nonce starts with the high word.
    [TestMethod]
    public void ComputeOriginalBlock_CounterAbove32Bits_FillsWordsTwelveAndThirteen()
    {
        byte[] key = Convert.FromHexString(SequentialKey);
        byte[] original = new byte[ChaCha20.BlockSize];
        byte[] rfc = new byte[ChaCha20.BlockSize];

        ChaCha20.ComputeOriginalBlock(key, Convert.FromHexString("0001020304050607"), 0x0000000a_0000000bUL, original);
        ChaCha20.ComputeBlock(key, Convert.FromHexString("0a0000000001020304050607"), 0x0000000b, rfc);

        CollectionAssert.AreEqual(rfc, original);
    }

    [TestMethod]
    public void ApplyOriginalKeyStream_CounterCrosses32Bits_CarriesIntoTheHighWord()
    {
        byte[] key = Convert.FromHexString(SequentialKey);
        byte[] nonce = Convert.FromHexString("0001020304050607");
        byte[] keyStream = new byte[2 * ChaCha20.BlockSize];
        byte[] expectedSecondBlock = new byte[ChaCha20.BlockSize];

        ChaCha20.ApplyOriginalKeyStream(key, nonce, uint.MaxValue, new byte[keyStream.Length], keyStream);
        ChaCha20.ComputeOriginalBlock(key, nonce, 0x1_0000_0000UL, expectedSecondBlock);

        CollectionAssert.AreEqual(expectedSecondBlock, keyStream[ChaCha20.BlockSize..]);
    }

    // The counter never wraps: a block at the largest counter is allowed, one past it throws.
    [TestMethod]
    public void ApplyKeyStream_LastBlockAtTheLargestCounter_Succeeds()
    {
        byte[] output = new byte[ChaCha20.BlockSize];
        byte[] expected = new byte[ChaCha20.BlockSize];

        ChaCha20.ApplyKeyStream(Convert.FromHexString(ZeroKey), Convert.FromHexString(ZeroNonce), uint.MaxValue, new byte[output.Length], output);
        ChaCha20.ComputeBlock(Convert.FromHexString(ZeroKey), Convert.FromHexString(ZeroNonce), uint.MaxValue, expected);

        CollectionAssert.AreEqual(expected, output);
    }

    [TestMethod]
    public void ApplyKeyStream_MessageRunsPastThe32BitCounter_ThrowsAndWritesNothing()
    {
        byte[] destination = new byte[ChaCha20.BlockSize + 1];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ChaCha20.ApplyKeyStream(Convert.FromHexString(ZeroKey), Convert.FromHexString(ZeroNonce), uint.MaxValue, new byte[destination.Length], destination));
        Assert.IsTrue(destination.All(value => value == 0));
    }

    [TestMethod]
    public void ApplyOriginalKeyStream_MessageRunsPastThe64BitCounter_Throws()
    {
        byte[] source = new byte[ChaCha20.BlockSize + 1];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ChaCha20.ApplyOriginalKeyStream(Convert.FromHexString(ZeroKey), Convert.FromHexString(ZeroOriginalNonce), ulong.MaxValue, source, source));
    }

    [TestMethod]
    public void ApplyOriginalKeyStream_LastBlockAtTheLargestCounter_Succeeds()
    {
        byte[] output = new byte[ChaCha20.BlockSize];
        byte[] expected = new byte[ChaCha20.BlockSize];

        ChaCha20.ApplyOriginalKeyStream(Convert.FromHexString(ZeroKey), Convert.FromHexString(ZeroOriginalNonce), ulong.MaxValue, new byte[output.Length], output);
        ChaCha20.ComputeOriginalBlock(Convert.FromHexString(ZeroKey), Convert.FromHexString(ZeroOriginalNonce), ulong.MaxValue, expected);

        CollectionAssert.AreEqual(expected, output);
    }

    [TestMethod]
    [DataRow(31, ChaCha20.NonceSize, 16, 16)]
    [DataRow(ChaCha20.KeySize, ChaCha20.OriginalNonceSize, 16, 16)]
    [DataRow(ChaCha20.KeySize, ChaCha20.NonceSize, 16, 15)]
    public void ApplyKeyStream_WrongLength_Throws(int keyLength, int nonceLength, int sourceLength, int destinationLength) =>
        Assert.ThrowsExactly<ArgumentException>(
            () => ChaCha20.ApplyKeyStream(new byte[keyLength], new byte[nonceLength], 0, new byte[sourceLength], new byte[destinationLength]));

    [TestMethod]
    [DataRow(31, ChaCha20.OriginalNonceSize, 16, 16)]
    [DataRow(ChaCha20.KeySize, ChaCha20.NonceSize, 16, 16)]
    [DataRow(ChaCha20.KeySize, ChaCha20.OriginalNonceSize, 16, 17)]
    public void ApplyOriginalKeyStream_WrongLength_Throws(int keyLength, int nonceLength, int sourceLength, int destinationLength) =>
        Assert.ThrowsExactly<ArgumentException>(
            () => ChaCha20.ApplyOriginalKeyStream(new byte[keyLength], new byte[nonceLength], 0, new byte[sourceLength], new byte[destinationLength]));

    [TestMethod]
    [DataRow(ChaCha20.OriginalNonceSize, ChaCha20.BlockSize)]
    [DataRow(ChaCha20.NonceSize, ChaCha20.BlockSize - 1)]
    public void ComputeBlock_WrongLength_Throws(int nonceLength, int blockLength) =>
        Assert.ThrowsExactly<ArgumentException>(
            () => ChaCha20.ComputeBlock(new byte[ChaCha20.KeySize], new byte[nonceLength], 0, new byte[blockLength]));

    [TestMethod]
    [DataRow(ChaCha20.NonceSize, ChaCha20.BlockSize)]
    [DataRow(ChaCha20.OriginalNonceSize, ChaCha20.BlockSize + 1)]
    public void ComputeOriginalBlock_WrongLength_Throws(int nonceLength, int blockLength) =>
        Assert.ThrowsExactly<ArgumentException>(
            () => ChaCha20.ComputeOriginalBlock(new byte[ChaCha20.KeySize], new byte[nonceLength], 0, new byte[blockLength]));
}
