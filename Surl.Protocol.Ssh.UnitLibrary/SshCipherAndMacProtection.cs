using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One direction's packets under a cipher and an HMAC: AES-CTR, or with
/// <c>--allow-weak-ssh-algorithms</c> AES-CBC, 3DES-CBC or RC4. With an encrypt-and-MAC MAC the
/// whole packet is encrypted and the MAC covers it in the clear (RFC 4253 section 6.4); with an
/// <c>-etm@openssh.com</c> MAC, <c>packet_length</c> is sent in the clear, only the rest is
/// encrypted, and the MAC covers the length and the ciphertext and is checked before anything
/// is decrypted (OpenSSH <c>PROTOCOL</c> section 1.7). A received MAC is compared in constant
/// time; one that does not match is <c>DISCONNECT</c> 5.
/// </summary>
/// <param name="cipher">The direction's cipher, which carries its state from packet to packet.</param>
/// <param name="mac">The direction's MAC.</param>
/// <param name="macKey">The direction's integrity key.</param>
internal sealed class SshCipherAndMacProtection(ISshPacketCipher cipher, SshHmac mac, byte[] macKey) : SshPacketProtection
{
    /// <inheritdoc/>
    public override int BlockSize => cipher.BlockSize;

    /// <inheritdoc/>
    public override bool EncryptsLength => !mac.EncryptThenMac;

    /// <inheritdoc/>
    public override bool AlignsLength => !mac.EncryptThenMac;

    /// <inheritdoc/>
    public override int TagLength => mac.MacLength;

    /// <inheritdoc/>
    public override byte[] OpenHead(uint sequenceNumber, byte[] head) => EncryptsLength ? cipher.Decrypt(head) : head;

    /// <inheritdoc/>
    public override byte[] OpenBody(uint sequenceNumber, byte[] plainHead, byte[] rest)
    {
        var ciphertext = rest.AsSpan(0, rest.Length - TagLength);
        var received = rest.AsSpan(rest.Length - TagLength);
        if (mac.EncryptThenMac)
        {
            Verify(sequenceNumber, [.. plainHead, .. ciphertext], received);

            return cipher.Decrypt(ciphertext);
        }

        byte[] packet = [.. plainHead, .. cipher.Decrypt(ciphertext)];
        Verify(sequenceNumber, packet, received);

        return packet[sizeof(uint)..];
    }

    /// <inheritdoc/>
    public override byte[] Seal(uint sequenceNumber, byte[] packet)
    {
        if (mac.EncryptThenMac)
        {
            byte[] sent = [.. packet.AsSpan(0, sizeof(uint)), .. cipher.Encrypt(packet.AsSpan(sizeof(uint)))];

            return [.. sent, .. mac.Compute(macKey, sequenceNumber, sent)];
        }

        return [.. cipher.Encrypt(packet), .. mac.Compute(macKey, sequenceNumber, packet)];
    }

    private void Verify(uint sequenceNumber, byte[] covered, ReadOnlySpan<byte> received)
    {
        if (!CryptographicOperations.FixedTimeEquals(mac.Compute(macKey, sequenceNumber, covered), received))
        {
            throw MacError(sequenceNumber);
        }
    }
}
