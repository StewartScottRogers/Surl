namespace Surl.Quic;

/// <summary>Derives the Initial secrets and keys of RFC 9001 Appendix A.1.</summary>
[TestClass]
public sealed class QuicInitialSecretsTests
{
    [TestMethod]
    public void Derive_Rfc9001AppendixADestinationConnectionId_GivesAppendixA1Secrets()
    {
        var connectionId = Rfc9001AppendixA.ClientDestinationConnectionIdBytes();

        var secrets = QuicInitialSecrets.Derive(connectionId);

        Assert.AreEqual(Rfc9001AppendixA.ClientInitialSecret, Convert.ToHexStringLower(secrets.Client));
        Assert.AreEqual(Rfc9001AppendixA.ServerInitialSecret, Convert.ToHexStringLower(secrets.Server));
    }

    [TestMethod]
    public void DerivePacketProtection_Rfc9001AppendixADestinationConnectionId_GivesAppendixA1KeysIvsAndHeaderProtectionKeys()
    {
        var connectionId = Rfc9001AppendixA.ClientDestinationConnectionIdBytes();

        var (client, server) = QuicInitialSecrets.DerivePacketProtection(connectionId);

        using (client)
        using (server)
        {
            Assert.AreEqual(QuicInitialSecrets.CipherSuite, client.CipherSuite);
            Assert.AreEqual(Rfc9001AppendixA.ClientKey, Convert.ToHexStringLower(client.Key.Span));
            Assert.AreEqual(Rfc9001AppendixA.ClientIv, Convert.ToHexStringLower(client.Iv.Span));
            Assert.AreEqual(Rfc9001AppendixA.ClientHeaderProtectionKey, Convert.ToHexStringLower(client.HeaderProtectionKey.Span));
            Assert.AreEqual(QuicInitialSecrets.CipherSuite, server.CipherSuite);
            Assert.AreEqual(Rfc9001AppendixA.ServerKey, Convert.ToHexStringLower(server.Key.Span));
            Assert.AreEqual(Rfc9001AppendixA.ServerIv, Convert.ToHexStringLower(server.Iv.Span));
            Assert.AreEqual(Rfc9001AppendixA.ServerHeaderProtectionKey, Convert.ToHexStringLower(server.HeaderProtectionKey.Span));
        }
    }
}
