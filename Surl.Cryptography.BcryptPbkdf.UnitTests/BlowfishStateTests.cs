using System.Buffers.Binary;

namespace Surl.Cryptography.BcryptPbkdf;

/// <summary>
/// Checks <see cref="BlowfishState" />'s pieces that the published vectors only reach
/// indirectly: the digits of pi it starts from and OpenBSD's <c>Blowfish_stream2word</c>,
/// and pins its cipher to Eric Young's Blowfish ECB test vectors.
/// </summary>
[TestClass]
public sealed class BlowfishStateTests
{
    // Eric Young's test vectors, published by Schneier at
    // https://www.schneier.com/wp-content/uploads/2015/12/vectors-2.txt, "ecb test data":
    // key bytes, clear bytes, cipher bytes, blocks read big-endian.
    [TestMethod]
    [DataRow("0000000000000000", "0000000000000000", "4EF997456198DD78")]
    [DataRow("FFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", "51866FD5B85ECB8A")]
    [DataRow("3000000000000000", "1000000000000001", "7D856F9A613063F2")]
    [DataRow("1111111111111111", "1111111111111111", "2466DD878B963C9D")]
    [DataRow("0123456789ABCDEF", "1111111111111111", "61F9C3802281B096")]
    [DataRow("1111111111111111", "0123456789ABCDEF", "7D0CC630AFDA1EC7")]
    [DataRow("FEDCBA9876543210", "0123456789ABCDEF", "0ACEAB0FC6A0A28D")]
    [DataRow("7CA110454A1A6E57", "01A1D6D039776742", "59C68245EB05282B")]
    [DataRow("0131D9619DC1376E", "5CD54CA83DEF57DA", "B1B8CC0B250F09A0")]
    public void EncryptAndDecrypt_EricYoungEcbVector_GiveThePublishedCipherAndClearBlocks(string key, string clear, string cipher)
    {
        BlowfishState state = new();
        state.Initialize();
        state.ExpandKey(Convert.FromHexString(key));
        (uint clearLeft, uint clearRight) = ReadBlock(clear);
        (uint cipherLeft, uint cipherRight) = ReadBlock(cipher);
        uint left = clearLeft;
        uint right = clearRight;

        state.Encrypt(ref left, ref right);

        Assert.AreEqual((cipherLeft, cipherRight), (left, right));

        state.Decrypt(ref left, ref right);

        Assert.AreEqual((clearLeft, clearRight), (left, right));
    }

    private static (uint Left, uint Right) ReadBlock(string hex)
    {
        byte[] block = Convert.FromHexString(hex);
        return (BinaryPrimitives.ReadUInt32BigEndian(block),
            BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(4)));
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

        uint first = BlowfishState.ReadWord([0x01, 0x02, 0x03], ref position);
        uint second = BlowfishState.ReadWord([0x01, 0x02, 0x03], ref position);

        Assert.AreEqual(0x01020301u, first);
        Assert.AreEqual(0x02030102u, second);
        Assert.AreEqual(2, position);
    }

    [TestMethod]
    public void ReadWord_EmptyData_IsZero()
    {
        int position = 0;

        uint word = BlowfishState.ReadWord([], ref position);

        Assert.AreEqual(0u, word);
    }

    [TestMethod]
    public void EncryptThenDecrypt_AfterKeySchedule_RoundTrips()
    {
        BlowfishState state = new();
        state.Initialize();
        state.ExpandKey("key"u8);
        uint left = 0x01234567;
        uint right = 0x89ABCDEF;

        state.Encrypt(ref left, ref right);
        state.Decrypt(ref left, ref right);

        Assert.AreEqual(0x01234567u, left);
        Assert.AreEqual(0x89ABCDEFu, right);
    }
}
