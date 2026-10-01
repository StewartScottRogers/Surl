using System.Buffers.Binary;
using System.Security.Cryptography;
using Surl.Cryptography.ChaCha20;
using Surl.Cryptography.Poly1305;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One direction's packets under <c>chacha20-poly1305@openssh.com</c> (OpenSSH's
/// <c>PROTOCOL.chacha20poly1305</c>), over the hand-built ChaCha20 and Poly1305 (ADR-0048).
/// The 64 bytes of key material are split into K_2, the first 32, for the payload, and K_1,
/// the last 32, for <c>packet_length</c>. The packet's sequence number, as a 64-bit big-endian
/// value, is the nonce of both. <c>packet_length</c> is encrypted with K_1 at block 0; the
/// Poly1305 key is the first 32 bytes of K_2's block 0, and the rest of the packet is encrypted
/// with K_2 from block 1. The 16-byte tag covers the encrypted length and the ciphertext and is
/// checked, in constant time, before anything after the length is decrypted. No MAC is agreed
/// beside it. A tag that does not verify is <c>DISCONNECT</c> 5.
/// </summary>
internal sealed class SshChaCha20Poly1305Protection : SshPacketProtection
{
    /// <summary>
    /// The cipher's name.
    /// </summary>
    public const string Name = "chacha20-poly1305@openssh.com";

    /// <summary>
    /// The length of the key material: K_2 then K_1.
    /// </summary>
    public const int KeyMaterialLength = 2 * ChaCha20.KeySize;

    private readonly byte[] payloadKey;
    private readonly byte[] lengthKey;

    /// <summary>
    /// Creates the protection.
    /// </summary>
    /// <param name="keyMaterial">The <see cref="KeyMaterialLength"/> bytes derived for the direction's key.</param>
    public SshChaCha20Poly1305Protection(byte[] keyMaterial)
    {
        payloadKey = keyMaterial[..ChaCha20.KeySize];
        lengthKey = keyMaterial[ChaCha20.KeySize..KeyMaterialLength];
    }

    /// <inheritdoc/>
    public override int BlockSize => 8;

    /// <inheritdoc/>
    public override bool EncryptsLength => true;

    /// <inheritdoc/>
    public override bool AlignsLength => false;

    /// <inheritdoc/>
    public override int TagLength => Poly1305.TagSize;

    /// <inheritdoc/>
    /// <remarks>Only the 4-byte <c>packet_length</c> is encrypted apart from the rest, so only it is read first.</remarks>
    public override int HeadLength => sizeof(uint);

    /// <inheritdoc/>
    public override byte[] OpenHead(uint sequenceNumber, byte[] head) => TransformLength(sequenceNumber, head);

    /// <inheritdoc/>
    public override byte[] OpenBody(uint sequenceNumber, byte[] plainHead, byte[] rest)
    {
        var ciphertext = rest.AsSpan(0, rest.Length - TagLength);
        byte[] covered = [.. TransformLength(sequenceNumber, plainHead), .. ciphertext];
        if (!CryptographicOperations.FixedTimeEquals(ComputeTag(sequenceNumber, covered), rest.AsSpan(ciphertext.Length)))
        {
            throw MacError(sequenceNumber);
        }

        var plaintext = new byte[ciphertext.Length];
        ChaCha20.ApplyOriginalKeyStream(payloadKey, Nonce(sequenceNumber), 1, ciphertext, plaintext);

        return plaintext;
    }

    /// <inheritdoc/>
    public override byte[] Seal(uint sequenceNumber, byte[] packet)
    {
        var sent = new byte[packet.Length + TagLength];
        TransformLength(sequenceNumber, packet[..sizeof(uint)]).CopyTo(sent, 0);
        ChaCha20.ApplyOriginalKeyStream(payloadKey, Nonce(sequenceNumber), 1, packet.AsSpan(sizeof(uint)), sent.AsSpan(sizeof(uint), packet.Length - sizeof(uint)));
        ComputeTag(sequenceNumber, sent[..packet.Length]).CopyTo(sent, packet.Length);

        return sent;
    }

    private static byte[] Nonce(uint sequenceNumber)
    {
        var nonce = new byte[ChaCha20.OriginalNonceSize];
        BinaryPrimitives.WriteUInt64BigEndian(nonce, sequenceNumber);

        return nonce;
    }

    private byte[] TransformLength(uint sequenceNumber, byte[] length)
    {
        var transformed = new byte[sizeof(uint)];
        ChaCha20.ApplyOriginalKeyStream(lengthKey, Nonce(sequenceNumber), 0, length, transformed);

        return transformed;
    }

    private byte[] ComputeTag(uint sequenceNumber, byte[] covered)
    {
        Span<byte> block = stackalloc byte[ChaCha20.BlockSize];
        try
        {
            ChaCha20.ComputeOriginalBlock(payloadKey, Nonce(sequenceNumber), 0, block);

            return Poly1305.ComputeTag(block[..Poly1305.KeySize], covered);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(block);
        }
    }
}
