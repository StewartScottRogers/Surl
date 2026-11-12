using System.Security.Cryptography;

namespace Surl.Tls13;

[TestClass]
public sealed class TlsKeyExchangeTests
{
    [TestMethod]
    public void Agree_Rfc8448Section3ClientShareWithTheTracesServerKey_ReturnsTheTracesServerShareAndSharedSecret()
    {
        var clientShare = ClientHello.Read(Rfc8448.ClientHello).KeyShares![0];
        using var random = new UnitTestScriptedRandom(Rfc8448.ServerEphemeralPrivateKey);

        var agreement = TlsKeyExchange.Agree(clientShare, random);

        CollectionAssert.AreEqual(Rfc8448.ServerHello[52..84], agreement.ServerShare);
        CollectionAssert.AreEqual(Rfc8448.SharedSecret, agreement.SharedSecret);
    }

    [TestMethod]
    public void Agree_GroupTheServerDoesNotServe_ThrowsArgumentOutOfRangeException()
    {
        var clientShare = new TlsKeyShareEntry((TlsNamedGroup)0x0102, new byte[512]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TlsKeyExchange.Agree(clientShare, RandomNumberGenerator.Create()));
    }
}
