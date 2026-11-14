using System.Security.Cryptography;

namespace Surl.Tls13;

/// <summary>Record protection against the protected records of RFC 8448 section 3, byte for byte, and the record rules of RFC 8446 section 5.</summary>
[TestClass]
public sealed class TlsRecordProtectionTests
{
    private const TlsCipherSuite Aes128 = TlsCipherSuite.Aes128GcmSha256;

    // RFC 8446 section 7.2 applied to Rfc8448.ServerApplicationTrafficSecret, computed here with
    // HKDF-Expand over the HkdfLabel bytes 0020 11 "tls13 traffic upd" 00 (independent of the library).
    private static readonly byte[] NextServerApplicationSecret = Convert.FromHexString(
        "51921b8aa3001976eb401d0a4319a8516416a6c56001a357e5d162031e84f916");

    private static byte[] Secret(TlsCipherSuite suite) =>
        Enumerable.Range(1, suite == TlsCipherSuite.Aes256GcmSha384 ? 48 : 32).Select(i => (byte)i).ToArray();

    [TestMethod]
    public void Protect_RfcServerHandshakeFlight_IsTheRfcRecord()
    {
        var protector = new TlsRecordProtector(Aes128, Rfc8448.ServerHandshakeTrafficSecret);

        var record = protector.Protect(TlsContentType.Handshake, Rfc8448Records.ServerHandshakePayload);

        CollectionAssert.AreEqual(Rfc8448Records.ServerHandshakeRecord, record);
        Assert.AreEqual(1UL, protector.SequenceNumber);
    }

    [TestMethod]
    public void Unprotect_RfcServerHandshakeFlight_IsThePayload()
    {
        var unprotector = new TlsRecordUnprotector(Aes128, Rfc8448.ServerHandshakeTrafficSecret);

        var inner = unprotector.Unprotect(Rfc8448Records.ServerHandshakeRecord);

        Assert.AreEqual(TlsContentType.Handshake, inner.ContentType);
        CollectionAssert.AreEqual(Rfc8448Records.ServerHandshakePayload, inner.Content);
        Assert.AreEqual(1UL, unprotector.SequenceNumber);
    }

    [TestMethod]
    public void ProtectAndUnprotect_RfcClientFinished_AreTheRfcRecord()
    {
        var protector = new TlsRecordProtector(Aes128, Rfc8448.ClientHandshakeTrafficSecret);
        var unprotector = new TlsRecordUnprotector(Aes128, Rfc8448.ClientHandshakeTrafficSecret);

        var record = protector.Protect(TlsContentType.Handshake, Rfc8448Records.ClientFinishedPayload);
        var inner = unprotector.Unprotect(Rfc8448Records.ClientFinishedRecord);

        CollectionAssert.AreEqual(Rfc8448Records.ClientFinishedRecord, record);
        CollectionAssert.AreEqual(Rfc8448Records.ClientFinishedPayload, inner.Content);
    }

    [TestMethod]
    public void ProtectAndUnprotect_RfcServerApplicationRecords_AdvanceTheSequenceNumber()
    {
        var protector = new TlsRecordProtector(Aes128, Rfc8448.ServerApplicationTrafficSecret);
        var unprotector = new TlsRecordUnprotector(Aes128, Rfc8448.ServerApplicationTrafficSecret);

        var ticket = protector.Protect(TlsContentType.Handshake, Rfc8448Records.ServerTicketPayload);
        var data = protector.Protect(TlsContentType.ApplicationData, Rfc8448Records.ServerAppPayload);
        var alert = protector.Protect(TlsContentType.Alert, Rfc8448Records.ServerAlertPayload);

        CollectionAssert.AreEqual(Rfc8448Records.ServerTicketRecord, ticket);
        CollectionAssert.AreEqual(Rfc8448Records.ServerAppRecord, data);
        CollectionAssert.AreEqual(Rfc8448Records.ServerAlertRecord, alert);
        Assert.AreEqual(3UL, protector.SequenceNumber);
        Assert.AreEqual(TlsContentType.Handshake, unprotector.Unprotect(Rfc8448Records.ServerTicketRecord).ContentType);
        CollectionAssert.AreEqual(Rfc8448Records.ServerAppPayload, unprotector.Unprotect(Rfc8448Records.ServerAppRecord).Content);
        var unprotectedAlert = unprotector.Unprotect(Rfc8448Records.ServerAlertRecord);
        Assert.AreEqual(TlsContentType.Alert, unprotectedAlert.ContentType);
        CollectionAssert.AreEqual(Rfc8448Records.ServerAlertPayload, unprotectedAlert.Content);
        Assert.AreEqual(3UL, unprotector.SequenceNumber);
    }

    [TestMethod]
    public void ProtectAndUnprotect_RfcClientApplicationRecords_AreTheRfcRecords()
    {
        var protector = new TlsRecordProtector(Aes128, Rfc8448.ClientApplicationTrafficSecret);
        var unprotector = new TlsRecordUnprotector(Aes128, Rfc8448.ClientApplicationTrafficSecret);

        var data = protector.Protect(TlsContentType.ApplicationData, Rfc8448Records.ClientAppPayload);
        var alert = protector.Protect(TlsContentType.Alert, Rfc8448Records.ClientAlertPayload);

        CollectionAssert.AreEqual(Rfc8448Records.ClientAppRecord, data);
        CollectionAssert.AreEqual(Rfc8448Records.ClientAlertRecord, alert);
        CollectionAssert.AreEqual(Rfc8448Records.ClientAppPayload, unprotector.Unprotect(Rfc8448Records.ClientAppRecord).Content);
        Assert.AreEqual(TlsContentType.Alert, unprotector.Unprotect(Rfc8448Records.ClientAlertRecord).ContentType);
    }

    [TestMethod]
    [DataRow(TlsCipherSuite.Aes128GcmSha256)]
    [DataRow(TlsCipherSuite.Aes256GcmSha384)]
    [DataRow(TlsCipherSuite.ChaCha20Poly1305Sha256)]
    public void Unprotect_ProtectedRecordWithPadding_ReturnsTheContentWithoutPadding(TlsCipherSuite suite)
    {
        var protector = new TlsRecordProtector(suite, Secret(suite));
        var unprotector = new TlsRecordUnprotector(suite, Secret(suite));
        byte[] content = [1, 2, 3, 0, 0];

        var padded = protector.Protect(TlsContentType.ApplicationData, content, 40);
        var plain = protector.Protect(TlsContentType.ApplicationData, content);

        Assert.AreEqual(plain.Length + 40, padded.Length);
        Assert.AreEqual(0x17, padded[0]);
        Assert.AreEqual(0x03, padded[1]);
        Assert.AreEqual(0x03, padded[2]);
        Assert.AreEqual(padded.Length - 5, (padded[3] << 8) | padded[4]);
        var inner = unprotector.Unprotect(padded);
        Assert.AreEqual(TlsContentType.ApplicationData, inner.ContentType);
        CollectionAssert.AreEqual(content, inner.Content);
        CollectionAssert.AreEqual(content, unprotector.Unprotect(plain).Content);
    }

    [TestMethod]
    public void Protect_EmptyContent_RoundTripsAsAnEmptyRecord()
    {
        var protector = new TlsRecordProtector(Aes128, Secret(Aes128));
        var unprotector = new TlsRecordUnprotector(Aes128, Secret(Aes128));

        var inner = unprotector.Unprotect(protector.Protect(TlsContentType.Alert, []));

        Assert.AreEqual(TlsContentType.Alert, inner.ContentType);
        Assert.AreEqual(0, inner.Content.Length);
    }

    [TestMethod]
    public void Protect_TheLargestContentAndPadding_IsAccepted()
    {
        var protector = new TlsRecordProtector(Aes128, Secret(Aes128));
        var unprotector = new TlsRecordUnprotector(Aes128, Secret(Aes128));
        var content = new byte[16384];
        content[^1] = 9;

        var record = protector.Protect(TlsContentType.ApplicationData, content, 239);

        Assert.AreEqual(5 + 16384 + 256, record.Length);
        CollectionAssert.AreEqual(content, unprotector.Unprotect(record).Content);
    }

    [TestMethod]
    public void Protect_ContentOverTwoToTheFourteen_Throws()
    {
        var protector = new TlsRecordProtector(Aes128, Secret(Aes128));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => protector.Protect(TlsContentType.ApplicationData, new byte[16385]));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(240)]
    public void Protect_PaddingThatBreaksTheCiphertextLimit_Throws(int padding)
    {
        var protector = new TlsRecordProtector(Aes128, Secret(Aes128));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => protector.Protect(TlsContentType.ApplicationData, new byte[16384], padding));
        Assert.AreEqual(0UL, protector.SequenceNumber);
    }

    [TestMethod]
    [DataRow(TlsCipherSuite.Aes128GcmSha256)]
    [DataRow(TlsCipherSuite.Aes256GcmSha384)]
    [DataRow(TlsCipherSuite.ChaCha20Poly1305Sha256)]
    public void Unprotect_ACorruptedTag_IsBadRecordMacAndKeepsTheSequenceNumber(TlsCipherSuite suite)
    {
        var protector = new TlsRecordProtector(suite, Secret(suite));
        var unprotector = new TlsRecordUnprotector(suite, Secret(suite));
        var record = protector.Protect(TlsContentType.Handshake, [1, 2, 3]);
        var good = (byte[])record.Clone();
        record[^1] ^= 1;

        var thrown = Assert.ThrowsExactly<TlsAlertException>(() => unprotector.Unprotect(record));

        Assert.AreEqual(TlsAlert.BadRecordMac, thrown.Alert);
        Assert.AreEqual(0UL, unprotector.SequenceNumber);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, unprotector.Unprotect(good).Content);
    }

    [TestMethod]
    public void Unprotect_ACorruptedHeader_IsBadRecordMac()
    {
        var protector = new TlsRecordProtector(Aes128, Secret(Aes128));
        var unprotector = new TlsRecordUnprotector(Aes128, Secret(Aes128));
        var record = protector.Protect(TlsContentType.Handshake, [1, 2, 3]);
        record[1] = 0x04;

        var thrown = Assert.ThrowsExactly<TlsAlertException>(() => unprotector.Unprotect(record));

        Assert.AreEqual(TlsAlert.BadRecordMac, thrown.Alert);
    }

    [TestMethod]
    public void Unprotect_ARecordOutOfOrder_IsBadRecordMac()
    {
        var protector = new TlsRecordProtector(Aes128, Secret(Aes128));
        var unprotector = new TlsRecordUnprotector(Aes128, Secret(Aes128));
        protector.Protect(TlsContentType.Handshake, [1]);
        var second = protector.Protect(TlsContentType.Handshake, [2]);

        var thrown = Assert.ThrowsExactly<TlsAlertException>(() => unprotector.Unprotect(second));

        Assert.AreEqual(TlsAlert.BadRecordMac, thrown.Alert);
    }

    [TestMethod]
    public void Unprotect_ABadRecordShape_IsTheAlertOfItsFault()
    {
        var protector = new TlsRecordProtector(Aes128, Secret(Aes128));
        var unprotector = new TlsRecordUnprotector(Aes128, Secret(Aes128));
        var good = protector.Protect(TlsContentType.Handshake, [1, 2, 3]);
        var wrongType = (byte[])good.Clone();
        wrongType[0] = 22;
        var tooLong = good.Concat(new byte[] { 0 }).ToArray();
        var tooShortBody = new byte[] { 0x17, 3, 3, 0, 16 }.Concat(new byte[16]).ToArray();
        var overflow = new byte[] { 0x17, 3, 3, 0x41, 0x01 }.Concat(new byte[16641]).ToArray();

        Assert.AreEqual(TlsAlert.DecodeError, Assert.ThrowsExactly<TlsAlertException>(() => unprotector.Unprotect(good[..4])).Alert);
        Assert.AreEqual(TlsAlert.UnexpectedMessage, Assert.ThrowsExactly<TlsAlertException>(() => unprotector.Unprotect(wrongType)).Alert);
        Assert.AreEqual(TlsAlert.DecodeError, Assert.ThrowsExactly<TlsAlertException>(() => unprotector.Unprotect(tooLong)).Alert);
        Assert.AreEqual(TlsAlert.DecodeError, Assert.ThrowsExactly<TlsAlertException>(() => unprotector.Unprotect(tooShortBody)).Alert);
        Assert.AreEqual(TlsAlert.RecordOverflow, Assert.ThrowsExactly<TlsAlertException>(() => unprotector.Unprotect(overflow)).Alert);
        Assert.AreEqual(0UL, unprotector.SequenceNumber);
    }

    [TestMethod]
    public void Unprotect_AnInnerPlaintextOfZerosOnly_IsUnexpectedMessage()
    {
        var thrown = Assert.ThrowsExactly<TlsAlertException>(() => UnprotectSealed(new byte[20]));

        Assert.AreEqual(TlsAlert.UnexpectedMessage, thrown.Alert);
    }

    [TestMethod]
    public void Unprotect_ContentOverTwoToTheFourteen_IsRecordOverflow()
    {
        var inner = new byte[16386];
        inner[16385] = 23;

        var thrown = Assert.ThrowsExactly<TlsAlertException>(() => UnprotectSealed(inner));

        Assert.AreEqual(TlsAlert.RecordOverflow, thrown.Alert);
    }

    [TestMethod]
    public void Unprotect_ContentOfExactlyTwoToTheFourteen_IsAccepted()
    {
        var inner = new byte[16385];
        inner[16384] = 23;

        Assert.AreEqual(16384, UnprotectSealed(inner).Content.Length);
    }

    [TestMethod]
    public void Seal_AtTheLastSequenceNumber_Refuses()
    {
        var keys = new TlsRecordKeys(Aes128, Secret(Aes128)) { SequenceNumber = ulong.MaxValue };

        Assert.ThrowsExactly<InvalidOperationException>(() => keys.Seal([], [1]));
    }

    [TestMethod]
    public void Seal_TheSequenceNumberBeforeTheLast_Works()
    {
        var keys = new TlsRecordKeys(Aes128, Secret(Aes128)) { SequenceNumber = ulong.MaxValue - 1 };

        keys.Seal([], [1]);

        Assert.AreEqual(ulong.MaxValue, keys.SequenceNumber);
    }

    [TestMethod]
    public void UpdateKeys_OnBothSides_FollowsTheSection72Derivation()
    {
        var protector = new TlsRecordProtector(Aes128, Rfc8448.ServerApplicationTrafficSecret);
        var unprotector = new TlsRecordUnprotector(Aes128, Rfc8448.ServerApplicationTrafficSecret);
        var fresh = new TlsRecordUnprotector(Aes128, NextServerApplicationSecret);
        protector.Protect(TlsContentType.Handshake, [1]);

        protector.UpdateKeys();
        unprotector.UpdateKeys();
        var record = protector.Protect(TlsContentType.ApplicationData, [4, 5, 6]);

        CollectionAssert.AreEqual(NextServerApplicationSecret, protector.TrafficSecret);
        CollectionAssert.AreEqual(NextServerApplicationSecret, unprotector.TrafficSecret);
        Assert.AreEqual(1UL, protector.SequenceNumber);
        Assert.AreEqual(0UL, unprotector.SequenceNumber);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, unprotector.Unprotect(record).Content);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, fresh.Unprotect(record).Content);
    }

    [TestMethod]
    public void UpdateKeys_Sha384Suite_DerivesASecretOfFortyEightBytes()
    {
        var protector = new TlsRecordProtector(TlsCipherSuite.Aes256GcmSha384, Secret(TlsCipherSuite.Aes256GcmSha384));
        var before = protector.TrafficSecret;

        protector.UpdateKeys();

        Assert.AreEqual(48, protector.TrafficSecret.Length);
        CollectionAssert.AreNotEqual(before, protector.TrafficSecret);
    }

    [TestMethod]
    public void Unprotect_ARecordFromBeforeTheKeyUpdate_IsBadRecordMac()
    {
        var protector = new TlsRecordProtector(Aes128, Secret(Aes128));
        var unprotector = new TlsRecordUnprotector(Aes128, Secret(Aes128));
        var old = protector.Protect(TlsContentType.Handshake, [1]);
        unprotector.UpdateKeys();

        var thrown = Assert.ThrowsExactly<TlsAlertException>(() => unprotector.Unprotect(old));

        Assert.AreEqual(TlsAlert.BadRecordMac, thrown.Alert);
    }

    [TestMethod]
    public void Protect_Aes256Gcm_MatchesTheBaseClassLibraryAesGcm()
    {
        var suite = TlsCipherSuite.Aes256GcmSha384;
        var protector = new TlsRecordProtector(suite, Secret(suite));
        var key = TlsKeySchedule.ExpandLabel(HashAlgorithmName.SHA384, Secret(suite), "key", [], 32);
        var iv = TlsKeySchedule.ExpandLabel(HashAlgorithmName.SHA384, Secret(suite), "iv", [], 12);

        var record = protector.Protect(TlsContentType.Handshake, [7, 8, 9]);

        var expected = new byte[4 + 16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(iv, new byte[] { 7, 8, 9, 22 }, expected.AsSpan(0, 4), expected.AsSpan(4), record[..5]);
        CollectionAssert.AreEqual(expected, record[5..]);
    }

    [TestMethod]
    public void Protect_ChaCha20Poly1305_MatchesTheBaseClassLibraryAead()
    {
        if (!ChaCha20Poly1305.IsSupported)
        {
            Assert.Inconclusive("This platform's base class library has no ChaCha20Poly1305.");
        }

        var suite = TlsCipherSuite.ChaCha20Poly1305Sha256;
        var protector = new TlsRecordProtector(suite, Secret(suite));
        var key = TlsKeySchedule.ExpandLabel(HashAlgorithmName.SHA256, Secret(suite), "key", [], 32);
        var iv = TlsKeySchedule.ExpandLabel(HashAlgorithmName.SHA256, Secret(suite), "iv", [], 12);
        var content = Enumerable.Range(0, 37).Select(i => (byte)i).ToArray();

        protector.Protect(TlsContentType.Handshake, [1]);
        var record = protector.Protect(TlsContentType.ApplicationData, content, 3);

        iv[11] ^= 1;
        var inner = content.Concat(new byte[] { 23, 0, 0, 0 }).ToArray();
        var expected = new byte[inner.Length + 16];
        using var aead = new ChaCha20Poly1305(key);
        aead.Encrypt(iv, inner, expected.AsSpan(0, inner.Length), expected.AsSpan(inner.Length), record[..5]);
        CollectionAssert.AreEqual(expected, record[5..]);
    }

    [TestMethod]
    public void NewProtector_WithASecretOfTheWrongLength_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new TlsRecordProtector(Aes128, new byte[48]));
        Assert.ThrowsExactly<ArgumentException>(() => new TlsRecordUnprotector(TlsCipherSuite.Aes256GcmSha384, new byte[32]));
    }

    [TestMethod]
    public void NewProtector_WithAnUnknownSuite_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TlsRecordProtector((TlsCipherSuite)0x1304, new byte[32]));
    }

    private static TlsInnerPlaintext UnprotectSealed(byte[] innerPlaintext)
    {
        var sender = new TlsRecordKeys(Aes128, Secret(Aes128));
        var length = innerPlaintext.Length + 16;
        byte[] header = [0x17, 3, 3, (byte)(length >> 8), (byte)length];
        var record = header.Concat(sender.Seal(header, innerPlaintext)).ToArray();
        return new TlsRecordUnprotector(Aes128, Secret(Aes128)).Unprotect(record);
    }
}
