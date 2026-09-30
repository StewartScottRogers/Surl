using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One direction's packets under AES-CTR and an HMAC. With an encrypt-and-MAC MAC the whole
/// packet is encrypted and the MAC covers it in the clear (RFC 4253 section 6.4); with an
/// <c>-etm@openssh.com</c> MAC, <c>packet_length</c> is sent in the clear, only the rest is
/// encrypted, and the MAC covers the length and the ciphertext and is checked before anything
/// is decrypted (OpenSSH <c>PROTOCOL</c> section 1.7). A received MAC is compared in constant
/// time; one that does not match is <c>DISCONNECT</c> 5.
/// </summary>
/// <param name="cipher">The direction's AES-CTR key stream.</param>
/// <param name="mac">The direction's MAC.</param>
/// <param name="macKey">The direction's integrity key.</param>
internal sealed class SshCipherAndMacProtection(SshAesCtr cipher, SshHmac mac, byte[] macKey) : SshPacketProtection
{
    /// <inheritdoc/>
    public override int BlockSize => SshAesCtr.BlockSize;

    /// <inheritdoc/>
    public override bool EncryptsLength => !mac.EncryptThenMac;

    /// <inheritdoc/>
    public override bool AlignsLength => !mac.EncryptThenMac;

    /// <inheritdoc/>
    public override int TagLength => mac.KeyLength;

    /// <summary>
    /// The key length of an AES-CTR cipher (RFC 4344, section 4).
    /// </summary>
    /// <param name="cipher">The cipher's name.</param>
    /// <returns>16, 24 or 32 for <c>aes128-ctr</c>, <c>aes192-ctr</c> and <c>aes256-ctr</c>; <see langword="null"/> for any other.</returns>
    public static int? KeyLengthFor(string cipher) => cipher switch
    {
        "aes128-ctr" => 16,
        "aes192-ctr" => 24,
        "aes256-ctr" => 32,
        _ => null,
    };

    /// <inheritdoc/>
    public override byte[] OpenHead(byte[] head) => EncryptsLength ? cipher.Transform(head) : head;

    /// <inheritdoc/>
    public override byte[] OpenBody(uint sequenceNumber, byte[] plainHead, byte[] rest)
    {
        var ciphertext = rest.AsSpan(0, rest.Length - TagLength);
        var received = rest.AsSpan(rest.Length - TagLength);
        if (mac.EncryptThenMac)
        {
            Verify(sequenceNumber, [.. plainHead, .. ciphertext], received);

            return cipher.Transform(ciphertext);
        }

        byte[] packet = [.. plainHead, .. cipher.Transform(ciphertext)];
        Verify(sequenceNumber, packet, received);

        return packet[sizeof(uint)..];
    }

    /// <inheritdoc/>
    public override byte[] Seal(uint sequenceNumber, byte[] packet)
    {
        if (mac.EncryptThenMac)
        {
            byte[] sent = [.. packet.AsSpan(0, sizeof(uint)), .. cipher.Transform(packet.AsSpan(sizeof(uint)))];

            return [.. sent, .. mac.Compute(macKey, sequenceNumber, sent)];
        }

        return [.. cipher.Transform(packet), .. mac.Compute(macKey, sequenceNumber, packet)];
    }

    private void Verify(uint sequenceNumber, byte[] covered, ReadOnlySpan<byte> received)
    {
        if (!CryptographicOperations.FixedTimeEquals(mac.Compute(macKey, sequenceNumber, covered), received))
        {
            throw MacError(sequenceNumber);
        }
    }
}
