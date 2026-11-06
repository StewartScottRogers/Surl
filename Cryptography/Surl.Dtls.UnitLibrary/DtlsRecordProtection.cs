using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.Dtls;

/// <summary>
/// One direction's AEAD protection of DTLS records (ADR-0087 decisions 2 and 8). The nonce is the
/// 4-byte implicit salt followed by the 8-byte explicit nonce, which is the record's epoch and
/// sequence number and travels at the start of the fragment (RFC 5288 section 3, RFC 6655
/// section 3). The additional data is RFC 5246 section 6.2.3.3's with <c>seq_num</c> replaced by
/// the epoch and sequence number (RFC 6347 section 4.1.2.1).
/// </summary>
public sealed class DtlsRecordProtection : IDisposable
{
    /// <summary>The length of the implicit salt from the key block: 4 bytes.</summary>
    public const int SaltSize = 4;

    /// <summary>The length of the explicit nonce sent in each record: 8 bytes.</summary>
    public const int ExplicitNonceSize = 8;

    private const int NonceSize = SaltSize + ExplicitNonceSize;
    private const int AdditionalDataSize = 13;

    private readonly byte[] salt;
    private readonly AesGcm? gcm;
    private readonly AesCcm? ccm;

    /// <summary>
    /// Creates the protection for one direction of one epoch.
    /// </summary>
    /// <param name="algorithm">The suite's AEAD algorithm.</param>
    /// <param name="key">The write key from the key block.</param>
    /// <param name="salt">The 4-byte implicit nonce (<c>write_IV</c>) from the key block.</param>
    /// <exception cref="ArgumentException">The salt is not 4 bytes, or the key's length does not suit the algorithm.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="algorithm"/> is not one of <see cref="DtlsAeadAlgorithm"/>'s.</exception>
    public DtlsRecordProtection(DtlsAeadAlgorithm algorithm, ReadOnlySpan<byte> key, ReadOnlySpan<byte> salt)
    {
        if (salt.Length != SaltSize)
        {
            throw new ArgumentException($"The implicit salt is {SaltSize} bytes.", nameof(salt));
        }

        this.salt = salt.ToArray();
        TagSize = algorithm switch
        {
            DtlsAeadAlgorithm.AesGcm => 16,
            DtlsAeadAlgorithm.AesCcm => 16,
            DtlsAeadAlgorithm.AesCcm8 => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Not a DTLS AEAD algorithm."),
        };

        if (algorithm == DtlsAeadAlgorithm.AesGcm)
        {
            gcm = new AesGcm(key, TagSize);
        }
        else
        {
            ccm = new AesCcm(key);
        }
    }

    /// <summary>The length of the authentication tag at the end of each fragment.</summary>
    public int TagSize { get; }

    /// <summary>How many bytes a protected fragment adds to its plaintext: the explicit nonce and the tag.</summary>
    public int Overhead => ExplicitNonceSize + TagSize;

    /// <summary>
    /// Protects <paramref name="plaintext"/> as the fragment of the record <paramref name="header"/> describes.
    /// </summary>
    /// <param name="header">The record's header; its <see cref="DtlsRecordHeader.Length"/> is ignored.</param>
    /// <param name="plaintext">The record's plaintext.</param>
    /// <returns>The fragment: explicit nonce, ciphertext and tag.</returns>
    public byte[] Seal(DtlsRecordHeader header, ReadOnlySpan<byte> plaintext)
    {
        var fragment = new byte[Overhead + plaintext.Length];
        var explicitNonce = fragment.AsSpan(0, ExplicitNonceSize);
        BinaryPrimitives.WriteUInt64BigEndian(
            explicitNonce, DtlsRecordHeader.EpochAndSequenceNumber(header.Epoch, header.SequenceNumber));

        Span<byte> nonce = stackalloc byte[NonceSize];
        Span<byte> additionalData = stackalloc byte[AdditionalDataSize];
        BuildNonce(explicitNonce, nonce);
        BuildAdditionalData(header, plaintext.Length, additionalData);

        var ciphertext = fragment.AsSpan(ExplicitNonceSize, plaintext.Length);
        var tag = fragment.AsSpan(ExplicitNonceSize + plaintext.Length);
        if (gcm is not null)
        {
            gcm.Encrypt(nonce, plaintext, ciphertext, tag, additionalData);
        }
        else
        {
            ccm!.Encrypt(nonce, plaintext, ciphertext, tag, additionalData);
        }

        return fragment;
    }

    /// <summary>
    /// Checks and decrypts the fragment of the record <paramref name="header"/> describes.
    /// </summary>
    /// <param name="header">The record's header as received.</param>
    /// <param name="fragment">The record's fragment: explicit nonce, ciphertext and tag.</param>
    /// <param name="plaintext">The plaintext, or empty when the record fails.</param>
    /// <returns><see langword="false"/> when the fragment is too short or its tag does not verify.</returns>
    public bool TryOpen(DtlsRecordHeader header, ReadOnlySpan<byte> fragment, out byte[] plaintext)
    {
        plaintext = [];
        if (fragment.Length < Overhead)
        {
            return false;
        }

        var opened = new byte[fragment.Length - Overhead];
        Span<byte> nonce = stackalloc byte[NonceSize];
        Span<byte> additionalData = stackalloc byte[AdditionalDataSize];
        BuildNonce(fragment[..ExplicitNonceSize], nonce);
        BuildAdditionalData(header, opened.Length, additionalData);

        var ciphertext = fragment.Slice(ExplicitNonceSize, opened.Length);
        var tag = fragment[(ExplicitNonceSize + opened.Length)..];
        try
        {
            Decrypt(nonce, ciphertext, tag, opened, additionalData);
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }

        plaintext = opened;
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        gcm?.Dispose();
        ccm?.Dispose();
    }

    private void Decrypt(
        ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext,
        ReadOnlySpan<byte> additionalData)
    {
        if (gcm is not null)
        {
            gcm.Decrypt(nonce, ciphertext, tag, plaintext, additionalData);
        }
        else
        {
            ccm!.Decrypt(nonce, ciphertext, tag, plaintext, additionalData);
        }
    }

    private void BuildNonce(ReadOnlySpan<byte> explicitNonce, Span<byte> nonce)
    {
        salt.CopyTo(nonce);
        explicitNonce.CopyTo(nonce[SaltSize..]);
    }

    private static void BuildAdditionalData(DtlsRecordHeader header, int plaintextLength, Span<byte> additionalData)
    {
        BinaryPrimitives.WriteUInt64BigEndian(
            additionalData, DtlsRecordHeader.EpochAndSequenceNumber(header.Epoch, header.SequenceNumber));
        additionalData[8] = (byte)header.ContentType;
        BinaryPrimitives.WriteUInt16BigEndian(additionalData[9..], header.Version);
        BinaryPrimitives.WriteUInt16BigEndian(additionalData[11..], (ushort)plaintextLength);
    }
}
