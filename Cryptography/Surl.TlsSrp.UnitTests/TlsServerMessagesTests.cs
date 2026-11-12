using Surl.Cryptography.Srp;

namespace Surl.TlsSrp;

/// <summary>RFC 5246 section 7.4 and RFC 5054 section 2.8.2: the messages the server writes.</summary>
[TestClass]
public sealed class TlsServerMessagesTests
{
    [TestMethod]
    public void ServerHello_NothingToAnswer_LeavesTheExtensionsBlockOut()
    {
        var negotiation = new TlsSrpNegotiation(TlsSrpCipherSuite.SrpShaAes128CbcSha, [1], false, false, false, null, null);

        var hello = TlsServerMessages.ServerHello(negotiation, UnitTestSrpClient.ServerRandomPattern);

        CollectionAssert.AreEqual(UnitTestSrpClient.ExpectedServerHello(0xC01D), hello);
        CollectionAssert.AreEqual(Convert.FromHexString("02000026"), hello[..4]);
    }

    [TestMethod]
    public void ServerKeyExchange_Rfc5054Group_WritesNGSAndBAsVectors()
    {
        var message = TlsServerMessages.ServerKeyExchange(SrpGroup.Rfc5054Group1024, [0xAA], [0x01, 0x02]);

        CollectionAssert.AreEqual(Convert.FromHexString("0C00008B" + "0080"), message[..6]);
        CollectionAssert.AreEqual(Convert.FromHexString("000102" + "01AA" + "00020102"), message[^9..]);
    }

    [TestMethod]
    public void SignedServerKeyExchange_ParamsSchemeAndSignature_AppendsTheDigitallySignedStruct()
    {
        var parameters = TlsServerMessages.ServerSrpParams(SrpGroup.Rfc5054Group1024, [0xAA], [0x01, 0x02]);

        var message = TlsServerMessages.SignedServerKeyExchange(parameters, TlsSignatureScheme.RsaPssRsaeSha256, [0x11, 0x22, 0x33]);

        CollectionAssert.AreEqual(Convert.FromHexString("0C"), message[..1]);
        Assert.AreEqual(message.Length - 4, (message[2] << 8) | message[3]);
        CollectionAssert.AreEqual(parameters, message[4..^7]);
        CollectionAssert.AreEqual(Convert.FromHexString("0804" + "0003" + "112233"), message[^7..]);
    }

    [TestMethod]
    public void Certificate_TwoCertificates_AreNestedInThreeByteLengths()
    {
        var message = TlsServerMessages.Certificate([[0xAA, 0xBB], [0xCC]]);

        CollectionAssert.AreEqual(Convert.FromHexString("0B00000C" + "000009" + "000002AABB" + "000001CC"), message);
    }

    [TestMethod]
    public void ServerHelloDoneAndFinished_AreFramedWithTheirTypes()
    {
        CollectionAssert.AreEqual(Convert.FromHexString("0E000000"), TlsServerMessages.ServerHelloDone());
        CollectionAssert.AreEqual(Convert.FromHexString("1400000201FF"), TlsServerMessages.Finished([0x01, 0xFF]));
    }

    [TestMethod]
    public void ServerHelloAndServerKeyExchange_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsServerMessages.ServerHello(null!, new byte[32]));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsServerMessages.ServerKeyExchange(null!, [1], [1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsServerMessages.ServerSrpParams(null!, [1], [1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsServerMessages.Certificate(null!));
    }
}
