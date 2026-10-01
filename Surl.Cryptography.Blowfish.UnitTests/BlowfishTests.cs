using System.Buffers.Binary;

namespace Surl.Cryptography.Blowfish;

/// <summary>
/// Pins <see cref="Blowfish" /> to Eric Young's Blowfish test vectors - the ECB table, the
/// set_key table and the CBC vector - and checks the pieces those vectors only reach
/// indirectly: the digits of pi it starts from, OpenBSD's <c>Blowfish_stream2word</c>, the
/// salted schedule bcrypt adds and the argument checks.
/// </summary>
[TestClass]
public sealed class BlowfishTests
{
    private const string SetKeyClearBlock = "FEDCBA9876543210";

    private const string SetKeyKeyBytes = "F0E1D2C3B4A5968778695A4B3C2D1E0F0011223344556677";

    // Eric Young's test vectors, published by Schneier at
    // https://www.schneier.com/wp-content/uploads/2015/12/vectors-2.txt, "ecb test data":
    // all 34 rows, in the file's order (row 7 repeats row 1, so each row carries its number);
    // key bytes, clear bytes, cipher bytes.
    [TestMethod]
    [DataRow(1, "0000000000000000", "0000000000000000", "4EF997456198DD78")]
    [DataRow(2, "FFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", "51866FD5B85ECB8A")]
    [DataRow(3, "3000000000000000", "1000000000000001", "7D856F9A613063F2")]
    [DataRow(4, "1111111111111111", "1111111111111111", "2466DD878B963C9D")]
    [DataRow(5, "0123456789ABCDEF", "1111111111111111", "61F9C3802281B096")]
    [DataRow(6, "1111111111111111", "0123456789ABCDEF", "7D0CC630AFDA1EC7")]
    [DataRow(7, "0000000000000000", "0000000000000000", "4EF997456198DD78")]
    [DataRow(8, "FEDCBA9876543210", "0123456789ABCDEF", "0ACEAB0FC6A0A28D")]
    [DataRow(9, "7CA110454A1A6E57", "01A1D6D039776742", "59C68245EB05282B")]
    [DataRow(10, "0131D9619DC1376E", "5CD54CA83DEF57DA", "B1B8CC0B250F09A0")]
    [DataRow(11, "07A1133E4A0B2686", "0248D43806F67172", "1730E5778BEA1DA4")]
    [DataRow(12, "3849674C2602319E", "51454B582DDF440A", "A25E7856CF2651EB")]
    [DataRow(13, "04B915BA43FEB5B6", "42FD443059577FA2", "353882B109CE8F1A")]
    [DataRow(14, "0113B970FD34F2CE", "059B5E0851CF143A", "48F4D0884C379918")]
    [DataRow(15, "0170F175468FB5E6", "0756D8E0774761D2", "432193B78951FC98")]
    [DataRow(16, "43297FAD38E373FE", "762514B829BF486A", "13F04154D69D1AE5")]
    [DataRow(17, "07A7137045DA2A16", "3BDD119049372802", "2EEDDA93FFD39C79")]
    [DataRow(18, "04689104C2FD3B2F", "26955F6835AF609A", "D887E0393C2DA6E3")]
    [DataRow(19, "37D06BB516CB7546", "164D5E404F275232", "5F99D04F5B163969")]
    [DataRow(20, "1F08260D1AC2465E", "6B056E18759F5CCA", "4A057A3B24D3977B")]
    [DataRow(21, "584023641ABA6176", "004BD6EF09176062", "452031C1E4FADA8E")]
    [DataRow(22, "025816164629B007", "480D39006EE762F2", "7555AE39F59B87BD")]
    [DataRow(23, "49793EBC79B3258F", "437540C8698F3CFA", "53C55F9CB49FC019")]
    [DataRow(24, "4FB05E1515AB73A7", "072D43A077075292", "7A8E7BFA937E89A3")]
    [DataRow(25, "49E95D6D4CA229BF", "02FE55778117F12A", "CF9C5D7A4986ADB5")]
    [DataRow(26, "018310DC409B26D6", "1D9D5C5018F728C2", "D1ABB290658BC778")]
    [DataRow(27, "1C587F1C13924FEF", "305532286D6F295A", "55CB3774D13EF201")]
    [DataRow(28, "0101010101010101", "0123456789ABCDEF", "FA34EC4847B268B2")]
    [DataRow(29, "1F1F1F1F0E0E0E0E", "0123456789ABCDEF", "A790795108EA3CAE")]
    [DataRow(30, "E0FEE0FEF1FEF1FE", "0123456789ABCDEF", "C39E072D9FAC631D")]
    [DataRow(31, "0000000000000000", "FFFFFFFFFFFFFFFF", "014933E0CDAFF6E4")]
    [DataRow(32, "FFFFFFFFFFFFFFFF", "0000000000000000", "F21E9A77B71C49BC")]
    [DataRow(33, "0123456789ABCDEF", "0000000000000000", "245946885754369A")]
    [DataRow(34, "FEDCBA9876543210", "FFFFFFFFFFFFFFFF", "6B5C5A9C5D9E0A5A")]
    public void EncryptBlockAndDecryptBlock_EricYoungEcbVector_GiveThePublishedCipherAndClearBlocks(int row, string key, string clear, string cipher)
    {
        Blowfish blowfish = new(Convert.FromHexString(key));
        byte[] block = new byte[Blowfish.BlockSize];

        blowfish.EncryptBlock(Convert.FromHexString(clear), block);

        Assert.AreEqual(cipher, Convert.ToHexString(block), $"ecb row {row}");

        blowfish.DecryptBlock(block, block);

        Assert.AreEqual(clear, Convert.ToHexString(block), $"ecb row {row}");
    }

    // vectors-2.txt, "set_key test data": the clear block FEDCBA9876543210 under the first
    // k bytes of F0E1D2C3B4A5968778695A4B3C2D1E0F0011223344556677. Keys of 1 to 3 bytes are
    // below Blowfish's 4-byte minimum, so the table is used from k = 4; k = 16 is
    // blowfish-cbc's 128-bit key (RFC 4253 section 6.3).
    [TestMethod]
    [DataRow(4, "BE1E639408640F05")]
    [DataRow(5, "B39E44481BDB1E6E")]
    [DataRow(6, "9457AA83B1928C0D")]
    [DataRow(7, "8BB77032F960629D")]
    [DataRow(8, "E87A244E2CC85E82")]
    [DataRow(9, "15750E7A4F4EC577")]
    [DataRow(10, "122BA70B3AB64AE0")]
    [DataRow(11, "3A833C9AFFC537F6")]
    [DataRow(12, "9409DA87A90F6BF2")]
    [DataRow(13, "884F80625060B8B4")]
    [DataRow(14, "1F85031C19E11968")]
    [DataRow(15, "79D9373A714CA34F")]
    [DataRow(16, "93142887EE3BE15C")]
    [DataRow(17, "03429E838CE2D14B")]
    [DataRow(18, "A4299E27469FF67B")]
    [DataRow(19, "AFD5AED1C1BC96A8")]
    [DataRow(20, "10851C0E3858DA9F")]
    [DataRow(21, "E6F51ED79B9DB21F")]
    [DataRow(22, "64A6E14AFD36B46F")]
    [DataRow(23, "80C7D7D45A5479AD")]
    [DataRow(24, "05044B62FA52D080")]
    public void EncryptBlockAndDecryptBlock_EricYoungSetKeyVector_GiveThePublishedCipherAndClearBlocks(int keyLength, string cipher)
    {
        Blowfish blowfish = new(Convert.FromHexString(SetKeyKeyBytes).AsSpan(0, keyLength));
        byte[] block = new byte[Blowfish.BlockSize];

        blowfish.EncryptBlock(Convert.FromHexString(SetKeyClearBlock), block);

        Assert.AreEqual(cipher, Convert.ToHexString(block));

        blowfish.DecryptBlock(block, block);

        Assert.AreEqual(SetKeyClearBlock, Convert.ToHexString(block));
    }

    // vectors-2.txt, "chaining mode test data": the 29-byte data, zero-padded to 32, chained
    // in CBC mode under the 16-byte key and the IV, one EncryptBlock per block.
    [TestMethod]
    public void EncryptBlock_ChainedInCbcMode_GivesTheEricYoungCbcCipherText()
    {
        Blowfish blowfish = new(Convert.FromHexString("0123456789ABCDEFF0E1D2C3B4A59687"));
        byte[] chain = Convert.FromHexString("FEDCBA9876543210");
        byte[] data = new byte[32];
        Convert.FromHexString("37363534333231204E6F77206973207468652074696D6520666F722000").CopyTo(data, 0);
        byte[] cipher = new byte[32];

        for (int offset = 0; offset < data.Length; offset += Blowfish.BlockSize)
        {
            for (int index = 0; index < Blowfish.BlockSize; index++)
            {
                chain[index] ^= data[offset + index];
            }

            blowfish.EncryptBlock(chain, chain);
            chain.CopyTo(cipher, offset);
        }

        Assert.AreEqual("6B77B4D63006DEE605B156E27403979358DEB9E7154616D959F1652BD5FF92CC", Convert.ToHexString(cipher));
    }

    // Schneier 1993: keys run to 56 bytes; vectors-2.txt stops at 24, so the longest key is
    // checked to round-trip and to differ from its 24-byte prefix, which the set_key table pins.
    [TestMethod]
    public void EncryptBlock_FiftySixByteKey_RoundTripsAndDiffersFromItsTwentyFourBytePrefix()
    {
        byte[] key = new byte[Blowfish.MaximumKeySize];
        Convert.FromHexString(SetKeyKeyBytes).CopyTo(key, 0);
        for (int index = 24; index < key.Length; index++)
        {
            key[index] = (byte)index;
        }

        Blowfish blowfish = new(key);
        byte[] block = new byte[Blowfish.BlockSize];

        blowfish.EncryptBlock(Convert.FromHexString(SetKeyClearBlock), block);

        Assert.AreNotEqual("05044B62FA52D080", Convert.ToHexString(block));

        blowfish.DecryptBlock(block, block);

        Assert.AreEqual(SetKeyClearBlock, Convert.ToHexString(block));
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(57)]
    public void Constructor_KeyOutsideFourToFiftySixBytes_Throws(int keyLength)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new Blowfish(new byte[keyLength]));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    [DataRow(7, 8, "source")]
    [DataRow(9, 8, "source")]
    [DataRow(8, 7, "destination")]
    public void EncryptBlock_SourceNotOneBlockOrDestinationTooShort_Throws(int sourceLength, int destinationLength, string parameterName)
    {
        Blowfish blowfish = new("key!"u8);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => blowfish.EncryptBlock(new byte[sourceLength], new byte[destinationLength]));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    public void DecryptBlock_SourceNotOneBlock_Throws()
    {
        Blowfish blowfish = new("key!"u8);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => blowfish.DecryptBlock(new byte[7], new byte[8]));

        Assert.AreEqual("source", exception.ParamName);
    }

    // vectors-2.txt, ecb row 5: the word-pair operations bcrypt uses give the same block as
    // EncryptBlock, halves read big-endian.
    [TestMethod]
    public void EncryptAndDecrypt_WordPair_GiveTheEricYoungEcbBlockAsBigEndianHalves()
    {
        Blowfish blowfish = new();
        blowfish.ExpandKey(Convert.FromHexString("0123456789ABCDEF"));
        uint left = 0x11111111;
        uint right = 0x11111111;

        blowfish.Encrypt(ref left, ref right);

        Assert.AreEqual((0x61F9C380u, 0x2281B096u), (left, right));

        blowfish.Decrypt(ref left, ref right);

        Assert.AreEqual((0x11111111u, 0x11111111u), (left, right));
    }

    // Empty salt data is the unsalted schedule (blf.c: Blowfish_expandstate with no data
    // exclusive-ors zero into every block), so it gives vectors-2.txt's ecb row 5.
    [TestMethod]
    public void ExpandKey_EmptySaltData_IsTheStandardSchedule()
    {
        Blowfish blowfish = new();
        blowfish.ExpandKey([], Convert.FromHexString("0123456789ABCDEF"));
        byte[] block = new byte[Blowfish.BlockSize];

        blowfish.EncryptBlock(Convert.FromHexString("1111111111111111"), block);

        Assert.AreEqual("61F9C3802281B096", Convert.ToHexString(block));
    }

    // The salted schedule's own output is pinned end to end by BcryptPbkdfTests' vectors
    // from OpenBSD's reference implementation; here, salt data changes the keyed state and
    // Initialize restores the unkeyed one.
    [TestMethod]
    public void ExpandKey_SaltData_ChangesTheStateAndInitializeRestoresTheDigitsOfPi()
    {
        byte[] key = Convert.FromHexString("0123456789ABCDEF");
        Blowfish salted = new();
        salted.ExpandKey("salt"u8, key);
        byte[] block = new byte[Blowfish.BlockSize];

        salted.EncryptBlock(Convert.FromHexString("1111111111111111"), block);

        Assert.AreNotEqual("61F9C3802281B096", Convert.ToHexString(block));

        salted.Initialize();
        salted.ExpandKey(key);
        salted.EncryptBlock(Convert.FromHexString("1111111111111111"), block);

        Assert.AreEqual("61F9C3802281B096", Convert.ToHexString(block));
    }

    [TestMethod]
    public void Clear_AfterKeying_ZeroesTheStateSoEncryptOnlySwapsTheHalves()
    {
        // With every subkey and S-box word zero, each round mixes in zero, so the block
        // comes out as its halves swapped.
        Blowfish blowfish = new(Convert.FromHexString("0123456789ABCDEF"));

        blowfish.Clear();
        uint left = 0x01234567;
        uint right = 0x89ABCDEF;
        blowfish.Encrypt(ref left, ref right);

        Assert.AreEqual((0x89ABCDEFu, 0x01234567u), (left, right));
    }

    // Schneier 1993: P1 and the first word of S1 are the first hexadecimal digits of pi's fraction; S4[255] ends the table.
    [TestMethod]
    public void PiDigits_FirstAndLastWords_AreThePublishedOnes()
    {
        Assert.HasCount(18, BlowfishPiDigits.Subkeys.ToArray());
        Assert.HasCount(1024, BlowfishPiDigits.SubstitutionBoxes.ToArray());
        Assert.AreEqual(0x243F6A88u, BlowfishPiDigits.Subkeys[0]);
        Assert.AreEqual(0x8979FB1Bu, BlowfishPiDigits.Subkeys[17]);
        Assert.AreEqual(0xD1310BA6u, BlowfishPiDigits.SubstitutionBoxes[0]);
        Assert.AreEqual(0x3AC372E6u, BlowfishPiDigits.SubstitutionBoxes[1023]);
    }

    [TestMethod]
    public void ReadWord_DataShorterThanAWord_WrapsToItsStartBigEndian()
    {
        int position = 0;

        uint first = Blowfish.ReadWord([0x01, 0x02, 0x03], ref position);
        uint second = Blowfish.ReadWord([0x01, 0x02, 0x03], ref position);

        Assert.AreEqual(0x01020301u, first);
        Assert.AreEqual(0x02030102u, second);
        Assert.AreEqual(2, position);
    }

    [TestMethod]
    public void ReadWord_EmptyData_IsZero()
    {
        int position = 0;

        uint word = Blowfish.ReadWord([], ref position);

        Assert.AreEqual(0u, word);
    }

    [TestMethod]
    public void ReadWord_FourBytes_IsTheBigEndianWord()
    {
        byte[] data = [0xDE, 0xAD, 0xBE, 0xEF];
        int position = 0;

        Assert.AreEqual(BinaryPrimitives.ReadUInt32BigEndian(data), Blowfish.ReadWord(data, ref position));
    }
}
