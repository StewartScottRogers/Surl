using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Each key exchange method and host-key algorithm BL-160 and BL-167 build, completed by
/// <see cref="SshTestKeyExchangeClient"/> against the server's handshake: the client verifies
/// the host key's signature over the H it computes itself, and both sides derive the same six
/// keys (RFC 4253 section 7.2, letters A to F).
/// </summary>
[TestClass]
public sealed class SshKeyExchangeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("curve25519-sha256")]
    [DataRow("curve25519-sha256@libssh.org")]
    [DataRow("ecdh-sha2-nistp256")]
    [DataRow("ecdh-sha2-nistp384")]
    [DataRow("ecdh-sha2-nistp521")]
    [DataRow("diffie-hellman-group14-sha256")]
    [DataRow("diffie-hellman-group16-sha512")]
    [DataRow("diffie-hellman-group18-sha512")]
    [DataRow("diffie-hellman-group-exchange-sha256")]
    public async Task EveryKeyExchangeMethod_IsCompletedWithAnRsaSha2_512Signature(string keyExchange)
    {
        await CompleteAsync(keyExchange, "rsa-sha2-512", SshTestKeys.AllHostKeys());
    }

    [TestMethod]
    [DataRow("rsa-sha2-512")]
    [DataRow("rsa-sha2-256")]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ecdsa-sha2-nistp384")]
    [DataRow("ecdsa-sha2-nistp521")]
    [DataRow("ssh-ed25519")]
    public async Task EveryHostKeyAlgorithm_SignsTheExchangeHash(string hostKeyAlgorithm)
    {
        await CompleteAsync("curve25519-sha256", hostKeyAlgorithm, SshTestKeys.AllHostKeys());
        await CompleteAsync("ecdh-sha2-nistp256", hostKeyAlgorithm, SshTestKeys.AllHostKeys());
        await CompleteAsync("diffie-hellman-group14-sha256", hostKeyAlgorithm, SshTestKeys.AllHostKeys());
    }

    [TestMethod]
    [DataRow("diffie-hellman-group14-sha256")]
    [DataRow("diffie-hellman-group-exchange-sha256")]
    public async Task PaddedClientValue_IsHashedAsSent(string keyExchange)
    {
        using var client = new SshTestKeyExchangeClient(keyExchange, "rsa-sha2-512", padClientValue: true);
        var connection = new InMemoryConnection([client.InboundBytes()]);

        var result = await Handshake(connection, SshTestKeys.AllHostKeys()).RunAsync(TestContext.CancellationToken);

        var (_, exchangeHash) = client.CheckServerAnswer(connection.WrittenBytes, SshHostKey.FromRsa(SshTestKeys.Rsa2048).PublicKeyBlob.Span);
        CollectionAssert.AreEqual(exchangeHash, result.SessionIdentifier);
    }

    [TestMethod]
    public async Task StrictKeyExchange_SetsBothSequenceNumbersBackToZeroAfterNewKeys()
    {
        using var client = new SshTestKeyExchangeClient("diffie-hellman-group-exchange-sha256", "rsa-sha2-512", strict: true);
        var handshake = Handshake(new InMemoryConnection([client.InboundBytes()]), SshTestKeys.AllHostKeys());

        var result = await handshake.RunAsync(TestContext.CancellationToken);

        Assert.IsTrue(result.Algorithms.StrictKeyExchange);
        Assert.AreEqual(0u, handshake.PacketWriter.SequenceNumber);
        Assert.AreEqual(0u, handshake.PacketReader!.SequenceNumber);
        Assert.IsFalse(handshake.PacketReader.RefusesSequenceWrap);
    }

    [TestMethod]
    public async Task NonStrictKeyExchange_KeepsCountingSequenceNumbersAfterNewKeys()
    {
        using var client = new SshTestKeyExchangeClient("diffie-hellman-group-exchange-sha256", "rsa-sha2-512");
        var handshake = Handshake(new InMemoryConnection([client.InboundBytes()]), SshTestKeys.AllHostKeys());

        var result = await handshake.RunAsync(TestContext.CancellationToken);

        Assert.IsFalse(result.Algorithms.StrictKeyExchange);
        Assert.AreEqual(4u, handshake.PacketWriter.SequenceNumber, "KEXINIT, GEX_GROUP, GEX_REPLY, NEWKEYS");
        Assert.AreEqual(4u, handshake.PacketReader!.SequenceNumber, "KEXINIT, GEX_REQUEST, GEX_INIT, NEWKEYS");
    }

    [TestMethod]
    public async Task IgnoreBetweenTheMethodMessagesAndNewKeys_IsSkippedWhenNotStrict()
    {
        using var client = new SshTestKeyExchangeClient("ecdh-sha2-nistp256", "rsa-sha2-256");
        var inbound = Concat(Ascii(ClientLine), Packet(client.KexInitPayload), client.MethodPackets(), Packet(Concat([2], String("x"))), Packet(21));
        var connection = new InMemoryConnection([inbound]);

        var result = await Handshake(connection, SshTestKeys.AllHostKeys()).RunAsync(TestContext.CancellationToken);

        var (_, exchangeHash) = client.CheckServerAnswer(connection.WrittenBytes, SshHostKey.FromRsa(SshTestKeys.Rsa2048).PublicKeyBlob.Span);
        CollectionAssert.AreEqual(exchangeHash, result.SessionIdentifier);
    }

    [TestMethod]
    public async Task WronglyGuessedPacketBeforeABuiltMethod_IsDiscardedAndTheExchangeCompletes()
    {
        using var client = new SshTestKeyExchangeClient("ecdh-sha2-nistp256", "rsa-sha2-512", firstKexPacketFollows: true);
        var guess = Packet(Concat([30], String("a guessed curve25519 key")));
        var chunks = new[] { Ascii(ClientLine), Packet(client.KexInitPayload), guess, client.MethodPackets(), Packet(21) };
        var connection = new YieldingConnection(chunks);

        var result = await Handshake(connection, SshTestKeys.AllHostKeys()).RunAsync(TestContext.CancellationToken);

        Assert.IsTrue(result.Algorithms.ClientGuessIsWrong);
        var (_, exchangeHash) = client.CheckServerAnswer(connection.WrittenBytes, SshHostKey.FromRsa(SshTestKeys.Rsa2048).PublicKeyBlob.Span);
        CollectionAssert.AreEqual(exchangeHash, result.SessionIdentifier);
    }

    [TestMethod]
    public async Task CompletedKeyExchange_IsAnsweredDisconnect11AfterNewKeys()
    {
        var log = new RecordingExchangeLog();
        using var client = new SshTestKeyExchangeClient("diffie-hellman-group-exchange-sha256", "rsa-sha2-512", strict: true, cipher: "aes128-cbc");
        var connection = new InMemoryConnection([client.InboundBytes()]);

        await Server(OfferWithAnUnbuiltCipher).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        var disconnect = ServerDisconnectPacket(11, "Packet protection not implemented");
        var written = connection.WrittenBytes;
        CollectionAssert.AreEqual(disconnect, written[^disconnect.Length..]);
        client.CheckServerAnswer(written[..^disconnect.Length], SshHostKey.FromRsa(SshTestKeys.Rsa2048).PublicKeyBlob.Span);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual(
            "The SSH packet protection aes128-cbc/aes128-cbc is not built yet; "
            + "the connection was ended after NEWKEYS.",
            log.Notes[2]);
        Assert.AreEqual("SSH disconnect sent: 11 Packet protection not implemented", log.Notes[3]);
    }

    [TestMethod]
    public async Task OfferNamingAHostKeyAlgorithmNoKeySigns_Throws()
    {
        using var client = new SshTestKeyExchangeClient("ecdh-sha2-nistp256", "ecdsa-sha2-nistp256");
        var offer = SshAlgorithmOffer.Default(["ecdsa-sha2-nistp256"], aesGcmIsSupported: false);
        var handshake = new SshTransportHandshake(
            new InMemoryConnection([client.InboundBytes()]),
            Context(TimeProvider.System, TestContext.CancellationToken),
            offer,
            RsaHostKeys,
            new SshSystemRandomSource());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => handshake.RunAsync(TestContext.CancellationToken));
    }

    private static SshTransportHandshake Handshake(IConnection connection, SshHostKeySet hostKeys) => new(
        connection,
        Context(TimeProvider.System, CancellationToken.None),
        SshAlgorithmOffer.Default(hostKeys.SignatureAlgorithms, aesGcmIsSupported: false),
        hostKeys,
        new SshSystemRandomSource());

    private async Task CompleteAsync(string keyExchange, string hostKeyAlgorithm, SshHostKeySet hostKeys)
    {
        using var client = new SshTestKeyExchangeClient(keyExchange, hostKeyAlgorithm);
        var connection = new InMemoryConnection([client.InboundBytes()]);

        var result = await Handshake(connection, hostKeys).RunAsync(TestContext.CancellationToken);

        var hostKey = hostKeys.Keys.Single(key => key.SignatureAlgorithms.Contains(hostKeyAlgorithm));
        var (sharedSecret, exchangeHash) = client.CheckServerAnswer(connection.WrittenBytes, hostKey.PublicKeyBlob.Span);
        Assert.AreEqual(keyExchange, result.Algorithms.KeyExchange);
        Assert.AreEqual(hostKeyAlgorithm, result.Algorithms.ServerHostKey);
        CollectionAssert.AreEqual(exchangeHash, result.SessionIdentifier);
        foreach (var letter in SshKeyDerivation.Letters)
        {
            CollectionAssert.AreEqual(
                client.DeriveKey(sharedSecret, exchangeHash, letter, 80),
                result.Keys.DeriveKey(letter, 80),
                $"Key {letter} of {keyExchange} with {hostKeyAlgorithm}");
        }
    }
}
