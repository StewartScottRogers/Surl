using System.Buffers.Binary;
using System.Security.Cryptography;
using Surl.Cryptography.ChaCha20;
using Surl.Cryptography.Poly1305;

namespace Surl.Quic;

/// <summary>
/// AEAD_CHACHA20_POLY1305 (RFC 8439 section 2.8) with ChaCha20 header protection (RFC 9001
/// section 5.4.4), on the hand-built <see cref="ChaCha20"/> and <see cref="Poly1305"/>
/// (ADR-0082 decision 3, ADR-0048).
/// </summary>
internal sealed class QuicChaCha20Poly1305PacketCipher(byte[] key, byte[] headerProtectionKey) : QuicPacketCipher
{
    private const int LengthsSize = 16;

    /// <inheritdoc/>
    public override void Seal(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData, Span<byte> ciphertext, Span<byte> tag)
    {
        ChaCha20.ApplyKeyStream(key, nonce, 1, plaintext, ciphertext);
        ComputeTag(nonce, associatedData, ciphertext, tag);
    }

    /// <inheritdoc/>
    public override bool TryOpen(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> associatedData, Span<byte> plaintext)
    {
        Span<byte> expectedTag = stackalloc byte[Poly1305.TagSize];
        ComputeTag(nonce, associatedData, ciphertext, expectedTag);
        if (!CryptographicOperations.FixedTimeEquals(expectedTag, tag))
        {
            return false;
        }

        ChaCha20.ApplyKeyStream(key, nonce, 1, ciphertext, plaintext);
        return true;
    }

    /// <summary>
    /// The first five bytes of ChaCha20's block for the header-protection key, with the sample's
    /// first four bytes as the little-endian block counter and its other twelve as the nonce.
    /// </summary>
    public override byte[] ComputeHeaderProtectionMask(ReadOnlySpan<byte> sample)
    {
        Span<byte> block = stackalloc byte[ChaCha20.BlockSize];
        ChaCha20.ComputeBlock(headerProtectionKey, sample[4..], BinaryPrimitives.ReadUInt32LittleEndian(sample), block);
        return block[..MaskLength].ToArray();
    }

    /// <summary>Holds no unmanaged resource: the keys belong to the <see cref="QuicPacketProtection"/> that made it.</summary>
    public override void Dispose()
    {
    }

    /// <summary>
    /// The Poly1305 tag of RFC 8439 section 2.8: keyed with block 0's first 32 bytes, over the
    /// associated data and the ciphertext, each padded to 16 bytes, then both lengths as 64-bit
    /// little-endian integers.
    /// </summary>
    private void ComputeTag(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> ciphertext, Span<byte> tag)
    {
        Span<byte> block = stackalloc byte[ChaCha20.BlockSize];
        ChaCha20.ComputeBlock(key, nonce, 0, block);
        var ciphertextOffset = PaddedLength(associatedData.Length);
        var lengthsOffset = ciphertextOffset + PaddedLength(ciphertext.Length);
        var macData = new byte[lengthsOffset + LengthsSize];
        associatedData.CopyTo(macData);
        ciphertext.CopyTo(macData.AsSpan(ciphertextOffset));
        BinaryPrimitives.WriteUInt64LittleEndian(macData.AsSpan(lengthsOffset), (ulong)associatedData.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(macData.AsSpan(lengthsOffset + 8), (ulong)ciphertext.Length);
        Poly1305.ComputeTag(block[..Poly1305.KeySize], macData, tag);
    }

    private static int PaddedLength(int length) => (length + 15) & ~15;
}
