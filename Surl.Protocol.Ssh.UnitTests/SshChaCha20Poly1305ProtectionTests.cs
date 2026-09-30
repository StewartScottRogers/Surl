namespace Surl.Protocol.Ssh;

/// <summary>
/// <see cref="SshChaCha20Poly1305Protection"/> against the worked example of
/// draft-ietf-sshm-chacha20-poly1305-04, Appendix A
/// (https://datatracker.ietf.org/doc/html/draft-ietf-sshm-chacha20-poly1305-04#appendix-A):
/// 64 bytes of key material, sequence number 7, one packet in the clear and the bytes sent.
/// </summary>
[TestClass]
public sealed class SshChaCha20Poly1305ProtectionTests
{
    private const uint SequenceNumber = 7;

    private static readonly byte[] KeyMaterial = Convert.FromHexString(
        "8bbff6855fc102338c373e73aac0c914f076a905b2444a32eecaffeae22becc5"
        + "e9b7a7a5825a8249346ec1c28301cf394543fc7569887d76e168f37562ac0740");

    private static readonly byte[] PlainPacket = Convert.FromHexString(
        "00000048065e00000000000000384c6f72656d20697073756d20646f6c6f7220"
        + "73697420616d65742c20636f6e73656374657475722061646970697369636"
        + "96e6720656c69744e43e804dc6c");

    private static readonly byte[] SentPacket = Convert.FromHexString(
        "2c3ecce4a5bc05895bf07a7ba956b6c68829ac7c83b780b7000ecde745afc705"
        + "bbc378ce03a280236b87b53bed5839662302b164b6286a48cd1e097138e3cb90"
        + "9b8b2b829dd18d2a35ff82d995349e855bf02c298ef775f2d1a7e8b8");

    [TestMethod]
    public void Seal_DraftAppendixAPacket_GivesTheDraftsBytes()
    {
        var protection = new SshChaCha20Poly1305Protection(KeyMaterial);

        var sent = protection.Seal(SequenceNumber, PlainPacket);

        CollectionAssert.AreEqual(SentPacket, sent);
    }

    [TestMethod]
    public void OpenHeadAndOpenBody_DraftAppendixABytes_GiveThePacketInTheClear()
    {
        var protection = new SshChaCha20Poly1305Protection(KeyMaterial);

        var plainHead = protection.OpenHead(SequenceNumber, SentPacket[..4]);
        var body = protection.OpenBody(SequenceNumber, plainHead, SentPacket[4..]);

        CollectionAssert.AreEqual(PlainPacket, (byte[])[.. plainHead, .. body]);
    }

    [TestMethod]
    public void OpenBody_DraftAppendixABytesUnderAnotherSequenceNumber_IsDisconnect5()
    {
        var protection = new SshChaCha20Poly1305Protection(KeyMaterial);
        var plainHead = protection.OpenHead(SequenceNumber, SentPacket[..4]);

        var refusal = Assert.ThrowsExactly<SshDisconnectRequiredException>(() => protection.OpenBody(SequenceNumber + 1, plainHead, SentPacket[4..]));

        Assert.AreEqual(SshDisconnectReason.MacError, refusal.Reason);
    }

    [TestMethod]
    public void Shape_IsAnEightByteBlockAeadWithItsLengthEncryptedApart()
    {
        var protection = new SshChaCha20Poly1305Protection(KeyMaterial);

        Assert.AreEqual(8, protection.BlockSize);
        Assert.AreEqual(4, protection.HeadLength);
        Assert.AreEqual(16, protection.TagLength);
        Assert.IsTrue(protection.EncryptsLength);
        Assert.IsFalse(protection.AlignsLength);
    }
}
