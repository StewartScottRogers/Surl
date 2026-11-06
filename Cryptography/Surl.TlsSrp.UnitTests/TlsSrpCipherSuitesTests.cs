namespace Surl.TlsSrp;

[TestClass]
public sealed class TlsSrpCipherSuitesTests
{
    [TestMethod]
    [DataRow(TlsSrpCipherSuite.SrpSha3DesEdeCbcSha, TlsBulkCipher.TripleDesEdeCbc)]
    [DataRow(TlsSrpCipherSuite.SrpShaRsa3DesEdeCbcSha, TlsBulkCipher.TripleDesEdeCbc)]
    [DataRow(TlsSrpCipherSuite.SrpShaDss3DesEdeCbcSha, TlsBulkCipher.TripleDesEdeCbc)]
    [DataRow(TlsSrpCipherSuite.SrpShaAes128CbcSha, TlsBulkCipher.Aes128Cbc)]
    [DataRow(TlsSrpCipherSuite.SrpShaRsaAes128CbcSha, TlsBulkCipher.Aes128Cbc)]
    [DataRow(TlsSrpCipherSuite.SrpShaDssAes128CbcSha, TlsBulkCipher.Aes128Cbc)]
    [DataRow(TlsSrpCipherSuite.SrpShaAes256CbcSha, TlsBulkCipher.Aes256Cbc)]
    [DataRow(TlsSrpCipherSuite.SrpShaRsaAes256CbcSha, TlsBulkCipher.Aes256Cbc)]
    [DataRow(TlsSrpCipherSuite.SrpShaDssAes256CbcSha, TlsBulkCipher.Aes256Cbc)]
    public void BulkCipherOf_EachRfc5054Suite_IsItsBlockCipher(TlsSrpCipherSuite suite, TlsBulkCipher expected)
    {
        Assert.AreEqual(expected, TlsSrpCipherSuites.BulkCipherOf(suite));
    }

    [TestMethod]
    public void BulkCipherOf_SuiteOutsideRfc5054_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TlsSrpCipherSuites.BulkCipherOf((TlsSrpCipherSuite)0x002F));
    }

    [TestMethod]
    [DataRow(TlsBulkCipher.TripleDesEdeCbc, 24, 8)]
    [DataRow(TlsBulkCipher.Aes128Cbc, 16, 16)]
    [DataRow(TlsBulkCipher.Aes256Cbc, 32, 16)]
    public void KeyAndBlockLength_EachCipher_AreItsSpecifications(TlsBulkCipher cipher, int keyLength, int blockLength)
    {
        Assert.AreEqual(keyLength, TlsSrpCipherSuites.KeyLengthOf(cipher));
        Assert.AreEqual(blockLength, TlsSrpCipherSuites.BlockLengthOf(cipher));
    }
}
