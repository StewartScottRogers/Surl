namespace Surl.TlsSrp;

/// <summary>
/// RFC 5246 section 6.3: the key block is <c>PRF(master_secret, "key expansion", server_random +
/// client_random)</c>, cut into client MAC key, server MAC key, client key, server key, and no IVs
/// for a TLS 1.2 CBC suite.
/// </summary>
[TestClass]
public sealed class TlsKeyBlockTests
{
    private static readonly byte[] MasterSecret = Enumerable.Range(0, 48).Select(value => (byte)(value * 3)).ToArray();
    private static readonly byte[] ClientRandom = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
    private static readonly byte[] ServerRandom = Enumerable.Range(0, 32).Select(value => (byte)(255 - value)).ToArray();

    [TestMethod]
    [DataRow(TlsSrpCipherSuite.SrpSha3DesEdeCbcSha, 88)]
    [DataRow(TlsSrpCipherSuite.SrpShaAes128CbcSha, 72)]
    [DataRow(TlsSrpCipherSuite.SrpShaAes256CbcSha, 104)]
    public void LengthFor_Suite_IsTwoMacKeysAndTwoEncryptionKeys(TlsSrpCipherSuite suite, int expected)
    {
        Assert.AreEqual(expected, TlsKeyBlock.LengthFor(suite));
    }

    [TestMethod]
    [DataRow(TlsSrpCipherSuite.SrpShaDss3DesEdeCbcSha, 24)]
    [DataRow(TlsSrpCipherSuite.SrpShaRsaAes128CbcSha, 16)]
    [DataRow(TlsSrpCipherSuite.SrpShaAes256CbcSha, 32)]
    public void Derive_Suite_CutsTheHandComputedKeyBlockInRfc5246Order(TlsSrpCipherSuite suite, int keyLength)
    {
        var expected = UnitTestPrf.Prf(MasterSecret, "key expansion", [.. ServerRandom, .. ClientRandom], 40 + (2 * keyLength));

        var keys = TlsKeyBlock.Derive(suite, MasterSecret, ClientRandom, ServerRandom);

        Assert.AreEqual(TlsSrpCipherSuites.BulkCipherOf(suite), keys.Cipher);
        CollectionAssert.AreEqual(expected[..20], keys.ClientMacKey);
        CollectionAssert.AreEqual(expected[20..40], keys.ServerMacKey);
        CollectionAssert.AreEqual(expected[40..(40 + keyLength)], keys.ClientKey);
        CollectionAssert.AreEqual(expected[(40 + keyLength)..], keys.ServerKey);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteProtections_EachDirection_UseThatDirectionsKeys(bool encryptThenMac)
    {
        var keys = TlsKeyBlock.Derive(TlsSrpCipherSuite.SrpShaAes256CbcSha, MasterSecret, ClientRandom, ServerRandom);
        using var ivSource = new UnitTestFixedRandomNumberGenerator(new byte[16]);
        using var clientWrites = keys.ClientWriteProtection(encryptThenMac, ivSource);
        using var serverReadsClient = new TlsCbcRecordProtection(
            TlsBulkCipher.Aes256Cbc, keys.ClientKey, keys.ClientMacKey, encryptThenMac, ivSource);
        using var serverWrites = keys.ServerWriteProtection(encryptThenMac, ivSource);
        using var clientReadsServer = new TlsCbcRecordProtection(
            TlsBulkCipher.Aes256Cbc, keys.ServerKey, keys.ServerMacKey, encryptThenMac, ivSource);
        byte[] message = [1, 2, 3];

        var fromClient = clientWrites.Protect(TlsContentType.Handshake, TlsRecordFormat.Tls12Version, message);
        var fromServer = serverWrites.Protect(TlsContentType.Handshake, TlsRecordFormat.Tls12Version, message);

        Assert.AreEqual(encryptThenMac, clientWrites.EncryptThenMac);
        CollectionAssert.AreEqual(message, serverReadsClient.Unprotect(TlsContentType.Handshake, TlsRecordFormat.Tls12Version, fromClient));
        CollectionAssert.AreEqual(message, clientReadsServer.Unprotect(TlsContentType.Handshake, TlsRecordFormat.Tls12Version, fromServer));
        CollectionAssert.AreNotEqual(fromClient, fromServer);
    }
}
