using System.Buffers.Binary;
using System.Security.Cryptography;
using Surl.Cryptography.ChaCha20;
using Surl.Cryptography.Poly1305;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The test client's own <c>chacha20-poly1305@openssh.com</c> for one direction, written out
/// step by step from OpenSSH's <c>PROTOCOL.chacha20poly1305</c> over the ChaCha20 and Poly1305
/// primitives, which their own test projects pin to RFC 8439's vectors. It shares no code with
/// the server's <see cref="SshChaCha20Poly1305Protection"/>.
/// </summary>
/// <param name="keyMaterial">The 64 bytes derived for the direction's key: K_2, then K_1.</param>
internal sealed class SshTestChaCha20Poly1305(byte[] keyMaterial)
{
    private readonly byte[] mainKey = keyMaterial[..32];
    private readonly byte[] headerKey = keyMaterial[32..64];

    /// <summary>
    /// Frames <paramref name="payload"/> with random padding that aligns everything after
    /// <c>packet_length</c> to 8 bytes, with at least 4 bytes of padding.
    /// </summary>
    /// <returns>The packet in the clear, <c>packet_length</c> first.</returns>
    public static byte[] Frame(byte[] payload)
    {
        var padding = 8 - ((1 + payload.Length) % 8);
        padding += padding < 4 ? 8 : 0;

        return Concat(UInt32((uint)(1 + payload.Length + padding)), [(byte)padding], payload, RandomNumberGenerator.GetBytes(padding));
    }

    /// <summary>
    /// Encrypts <paramref name="packet"/> as packet <paramref name="sequenceNumber"/> and appends its tag.
    /// </summary>
    public byte[] Seal(uint sequenceNumber, byte[] packet)
    {
        var sentLength = OpenLength(sequenceNumber, packet[..4]);
        var ciphertext = new byte[packet.Length - 4];
        ChaCha20.ApplyOriginalKeyStream(mainKey, Nonce(sequenceNumber), 1, packet.AsSpan(4), ciphertext);
        var sent = Concat(sentLength, ciphertext);

        return Concat(sent, Tag(sequenceNumber, sent));
    }

    /// <summary>
    /// Decrypts the 4-byte <c>packet_length</c> as sent; the same operation encrypts it.
    /// </summary>
    public byte[] OpenLength(uint sequenceNumber, byte[] sentLength)
    {
        var length = new byte[4];
        ChaCha20.ApplyOriginalKeyStream(headerKey, Nonce(sequenceNumber), 0, sentLength, length);

        return length;
    }

    /// <summary>
    /// Checks the tag after the ciphertext, failing the test if it does not verify, and decrypts.
    /// </summary>
    public byte[] OpenRest(uint sequenceNumber, byte[] sentLength, byte[] rest)
    {
        var ciphertext = rest[..^16];
        CollectionAssert.AreEqual(Tag(sequenceNumber, Concat(sentLength, ciphertext)), rest[^16..], "The server's tag does not verify.");
        var plaintext = new byte[ciphertext.Length];
        ChaCha20.ApplyOriginalKeyStream(mainKey, Nonce(sequenceNumber), 1, ciphertext, plaintext);

        return plaintext;
    }

    private static byte[] Nonce(uint sequenceNumber)
    {
        var nonce = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(nonce, sequenceNumber);

        return nonce;
    }

    private byte[] Tag(uint sequenceNumber, byte[] covered)
    {
        var block = new byte[64];
        ChaCha20.ComputeOriginalBlock(mainKey, Nonce(sequenceNumber), 0, block);

        return Poly1305.ComputeTag(block[..32], covered);
    }
}
