using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>
/// RFC 5246 section 6.2.3.2's CBC records (MAC-then-encrypt) and RFC 7366's encrypt-then-MAC, with
/// the sequence number in the MAC (section 6.2.3.1) and bad MAC and bad padding answered alike.
/// </summary>
[TestClass]
public sealed class TlsCbcRecordProtectionTests
{
    private static readonly byte[] Aes128Key = Enumerable.Range(0x10, 16).Select(value => (byte)value).ToArray();
    private static readonly byte[] MacKey = Enumerable.Range(0x40, 20).Select(value => (byte)value).ToArray();
    private static readonly byte[] Iv = Enumerable.Repeat((byte)0xA5, 16).ToArray();

    [TestMethod]
    public void Protect_MacThenEncrypt_IsTheIvThenCbcOfContentMacAndPadding()
    {
        byte[] content = [0x68, 0x65, 0x6C, 0x6C, 0x6F];
        var mac = UnitTestCbcRecords.Mac(MacKey, 0, TlsContentType.ApplicationData, content);
        var expected = UnitTestCbcRecords.MacThenEncrypt(
            TlsBulkCipher.Aes128Cbc, Aes128Key, Iv, content, mac, UnitTestCbcRecords.Padding(7));
        using var protection = Create(TlsBulkCipher.Aes128Cbc, Aes128Key, encryptThenMac: false);

        var fragment = protection.Protect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, content);

        CollectionAssert.AreEqual(expected, fragment);
        Assert.AreEqual(1UL, protection.SequenceNumber);
    }

    [TestMethod]
    public void Protect_EncryptThenMac_IsTheIvAndCiphertextThenTheirMac()
    {
        byte[] content = [0x68, 0x65, 0x6C, 0x6C, 0x6F];
        var expected = UnitTestCbcRecords.EncryptThenMac(
            TlsBulkCipher.Aes128Cbc, Aes128Key, MacKey, 0, TlsContentType.ApplicationData, Iv, content, UnitTestCbcRecords.Padding(11));
        using var protection = Create(TlsBulkCipher.Aes128Cbc, Aes128Key, encryptThenMac: true);

        var fragment = protection.Protect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, content);

        CollectionAssert.AreEqual(expected, fragment);
    }

    [TestMethod]
    [DataRow(TlsBulkCipher.TripleDesEdeCbc, false)]
    [DataRow(TlsBulkCipher.TripleDesEdeCbc, true)]
    [DataRow(TlsBulkCipher.Aes128Cbc, false)]
    [DataRow(TlsBulkCipher.Aes128Cbc, true)]
    [DataRow(TlsBulkCipher.Aes256Cbc, false)]
    [DataRow(TlsBulkCipher.Aes256Cbc, true)]
    public void Unprotect_RecordsInOrder_RoundTripEveryLength(TlsBulkCipher cipher, bool encryptThenMac)
    {
        var key = KeyFor(cipher);
        using var writer = Create(cipher, key, encryptThenMac);
        using var reader = Create(cipher, key, encryptThenMac);

        foreach (var length in new[] { 0, 1, 7, 8, 15, 16, 100, TlsRecordFormat.MaxPlaintextLength })
        {
            var content = Enumerable.Range(0, length).Select(value => (byte)value).ToArray();
            var fragment = writer.Protect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, content);

            CollectionAssert.AreEqual(content, reader.Unprotect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, fragment));
        }

        Assert.AreEqual(8UL, reader.SequenceNumber);
    }

    [TestMethod]
    public void Unprotect_MacThenEncryptWithWrongMac_IsBadRecordMac()
    {
        byte[] content = [1, 2, 3];
        var wrongMac = UnitTestCbcRecords.Mac(MacKey, 1, TlsContentType.ApplicationData, content);
        var fragment = UnitTestCbcRecords.MacThenEncrypt(
            TlsBulkCipher.Aes128Cbc, Aes128Key, Iv, content, wrongMac, UnitTestCbcRecords.Padding(9));

        AssertBadRecordMac(encryptThenMac: false, fragment);
    }

    [TestMethod]
    public void Unprotect_MacThenEncryptWithWrongPaddingByte_IsBadRecordMacAlike()
    {
        byte[] content = [1, 2, 3];
        var mac = UnitTestCbcRecords.Mac(MacKey, 0, TlsContentType.ApplicationData, content);
        var padding = UnitTestCbcRecords.Padding(9);
        padding[0] = 7;
        var fragment = UnitTestCbcRecords.MacThenEncrypt(TlsBulkCipher.Aes128Cbc, Aes128Key, Iv, content, mac, padding);

        AssertBadRecordMac(encryptThenMac: false, fragment);
    }

    [TestMethod]
    public void Unprotect_MacThenEncryptWithPaddingLongerThanTheRecord_IsBadRecordMac()
    {
        byte[] content = [1, 2, 3];
        var mac = UnitTestCbcRecords.Mac(MacKey, 0, TlsContentType.ApplicationData, content);
        var padding = Enumerable.Repeat((byte)200, 9).ToArray();
        var fragment = UnitTestCbcRecords.MacThenEncrypt(TlsBulkCipher.Aes128Cbc, Aes128Key, Iv, content, mac, padding);

        AssertBadRecordMac(encryptThenMac: false, fragment);
    }

    [TestMethod]
    public void Unprotect_EncryptThenMacWithWrongMac_IsBadRecordMac()
    {
        var fragment = UnitTestCbcRecords.EncryptThenMac(
            TlsBulkCipher.Aes128Cbc, Aes128Key, MacKey, 0, TlsContentType.ApplicationData, Iv, [1, 2, 3], UnitTestCbcRecords.Padding(13));
        fragment[^1] ^= 1;

        AssertBadRecordMac(encryptThenMac: true, fragment);
    }

    [TestMethod]
    public void Unprotect_EncryptThenMacWithWrongPaddingByte_IsBadRecordMacAlike()
    {
        var padding = UnitTestCbcRecords.Padding(13);
        padding[3] = 0;
        var fragment = UnitTestCbcRecords.EncryptThenMac(
            TlsBulkCipher.Aes128Cbc, Aes128Key, MacKey, 0, TlsContentType.ApplicationData, Iv, [1, 2, 3], padding);

        AssertBadRecordMac(encryptThenMac: true, fragment);
    }

    [TestMethod]
    public void Unprotect_EncryptThenMacWithPaddingLongerThanTheRecord_IsBadRecordMac()
    {
        var padding = Enumerable.Repeat((byte)100, 13).ToArray();
        var fragment = UnitTestCbcRecords.EncryptThenMac(
            TlsBulkCipher.Aes128Cbc, Aes128Key, MacKey, 0, TlsContentType.ApplicationData, Iv, [1, 2, 3], padding);

        AssertBadRecordMac(encryptThenMac: true, fragment);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Unprotect_ReplayedRecord_IsBadRecordMac(bool encryptThenMac)
    {
        using var writer = Create(TlsBulkCipher.Aes128Cbc, Aes128Key, encryptThenMac);
        using var reader = Create(TlsBulkCipher.Aes128Cbc, Aes128Key, encryptThenMac);
        var fragment = writer.Protect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, [1, 2, 3]);
        reader.Unprotect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, fragment);

        var alert = Assert.ThrowsExactly<TlsAlertException>(
            () => reader.Unprotect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, fragment));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert.Description);
        Assert.AreEqual(1UL, reader.SequenceNumber);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Unprotect_RecordUnderAnotherContentType_IsBadRecordMac(bool encryptThenMac)
    {
        using var writer = Create(TlsBulkCipher.Aes128Cbc, Aes128Key, encryptThenMac);
        var fragment = writer.Protect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, [1, 2, 3]);
        using var reader = Create(TlsBulkCipher.Aes128Cbc, Aes128Key, encryptThenMac);

        var alert = Assert.ThrowsExactly<TlsAlertException>(
            () => reader.Unprotect(TlsContentType.Handshake, UnitTestCbcRecords.Version, fragment));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert.Description);
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(false, 16)]
    [DataRow(false, 32)]
    [DataRow(false, 47)]
    [DataRow(true, 51)]
    [DataRow(true, 52)]
    [DataRow(true, 60)]
    public void Unprotect_FragmentOfImpossibleLength_IsBadRecordMac(bool encryptThenMac, int length)
    {
        AssertBadRecordMac(encryptThenMac, new byte[length]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Unprotect_PlaintextOver2To14Bytes_IsRecordOverflow(bool encryptThenMac)
    {
        using var writer = Create(TlsBulkCipher.Aes128Cbc, Aes128Key, encryptThenMac);
        var fragment = writer.Protect(
            TlsContentType.ApplicationData, UnitTestCbcRecords.Version, new byte[TlsRecordFormat.MaxPlaintextLength + 1]);
        using var reader = Create(TlsBulkCipher.Aes128Cbc, Aes128Key, encryptThenMac);

        var alert = Assert.ThrowsExactly<TlsAlertException>(
            () => reader.Unprotect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, fragment));

        Assert.AreEqual(TlsAlertDescription.RecordOverflow, alert.Description);
        Assert.AreEqual(0UL, reader.SequenceNumber);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        using var ivSource = new UnitTestFixedRandomNumberGenerator(Iv);

        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsCbcRecordProtection(TlsBulkCipher.Aes128Cbc, null!, MacKey, false, ivSource));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsCbcRecordProtection(TlsBulkCipher.Aes128Cbc, Aes128Key, null!, false, ivSource));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsCbcRecordProtection(TlsBulkCipher.Aes128Cbc, Aes128Key, MacKey, false, null!));
    }

    private static void AssertBadRecordMac(bool encryptThenMac, byte[] fragment)
    {
        using var protection = Create(TlsBulkCipher.Aes128Cbc, Aes128Key, encryptThenMac);

        var alert = Assert.ThrowsExactly<TlsAlertException>(
            () => protection.Unprotect(TlsContentType.ApplicationData, UnitTestCbcRecords.Version, fragment));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert.Description);
        Assert.AreEqual(0UL, protection.SequenceNumber);
    }

    private static byte[] KeyFor(TlsBulkCipher cipher) =>
        Enumerable.Range(1, TlsSrpCipherSuites.KeyLengthOf(cipher)).Select(value => (byte)(value * 7)).ToArray();

    private static TlsCbcRecordProtection Create(TlsBulkCipher cipher, byte[] key, bool encryptThenMac) =>
        new(cipher, key, MacKey, encryptThenMac, new UnitTestFixedRandomNumberGenerator(Iv));
}
