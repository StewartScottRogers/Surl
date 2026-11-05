using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// AEAD_AES_128_GCM or AEAD_AES_256_GCM, by key length, with AES-ECB header protection
/// (RFC 9001 sections 5.3 and 5.4.3): the base class library's <see cref="AesGcm"/> and
/// <c>Aes.EncryptEcb</c> (ADR-0082 decision 3).
/// </summary>
internal sealed class QuicAesGcmPacketCipher : QuicPacketCipher
{
    private readonly AesGcm aead;
    private readonly Aes headerProtection;

    public QuicAesGcmPacketCipher(byte[] key, byte[] headerProtectionKey)
    {
        aead = new AesGcm(key, QuicPacketProtection.AeadTagLength);
        headerProtection = Aes.Create();
        headerProtection.Key = headerProtectionKey;
    }

    /// <inheritdoc/>
    public override void Seal(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData, Span<byte> ciphertext, Span<byte> tag) =>
        aead.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

    /// <inheritdoc/>
    public override bool TryOpen(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> associatedData, Span<byte> plaintext)
    {
        try
        {
            aead.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public override byte[] ComputeHeaderProtectionMask(ReadOnlySpan<byte> sample) =>
        headerProtection.EncryptEcb(sample, PaddingMode.None)[..MaskLength];

    /// <inheritdoc/>
    public override void Dispose()
    {
        aead.Dispose();
        headerProtection.Dispose();
    }
}
