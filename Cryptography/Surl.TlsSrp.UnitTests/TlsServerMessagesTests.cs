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
    }
}
