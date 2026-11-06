using Surl.Tls13;

namespace Surl.Quic;

/// <summary>
/// The AEAD and the header-protection cipher of one cipher suite, keyed for one direction
/// (RFC 9001 sections 5.3 and 5.4). Every tag is <see cref="QuicPacketProtection.AeadTagLength"/> bytes.
/// </summary>
internal abstract class QuicPacketCipher : IDisposable
{
    /// <summary>How many bytes of the header-protection mask QUIC uses: one for the first byte, four for the packet number.</summary>
    public const int MaskLength = 5;

    /// <summary>Returns the cipher for a suite: hand-built ChaCha20-Poly1305, or the BCL's AES-GCM and AES-ECB.</summary>
    public static QuicPacketCipher Create(TlsCipherSuite cipherSuite, byte[] key, byte[] headerProtectionKey) =>
        cipherSuite == TlsCipherSuite.ChaCha20Poly1305Sha256
            ? new QuicChaCha20Poly1305PacketCipher(key, headerProtectionKey)
            : new QuicAesGcmPacketCipher(key, headerProtectionKey);

    /// <summary>Encrypts <paramref name="plaintext"/> into <paramref name="ciphertext"/> and writes the tag that authenticates it and <paramref name="associatedData"/>.</summary>
    public abstract void Seal(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData, Span<byte> ciphertext, Span<byte> tag);

    /// <summary>Decrypts <paramref name="ciphertext"/> into <paramref name="plaintext"/> when <paramref name="tag"/> authenticates it and <paramref name="associatedData"/>; returns <see langword="false"/> when it does not.</summary>
    public abstract bool TryOpen(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> associatedData, Span<byte> plaintext);

    /// <summary>Returns the <see cref="MaskLength"/>-byte header-protection mask for a 16-byte sample of the ciphertext.</summary>
    public abstract byte[] ComputeHeaderProtectionMask(ReadOnlySpan<byte> sample);

    /// <inheritdoc/>
    public abstract void Dispose();
}
