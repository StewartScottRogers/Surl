using System.Buffers.Binary;
using System.Security.Cryptography;
using Surl.Cryptography.ChaCha20;
using Surl.Cryptography.Poly1305;

namespace Surl.Tls13;

/// <summary>
/// One direction's record protection state (RFC 8446 section 5.3): the AEAD key and IV expanded
/// from a traffic secret, the sequence number, and the key update of section 7.2. The protector
/// and the unprotector each hold one.
/// </summary>
internal sealed class TlsRecordKeys
{
    internal const int HeaderLength = 5;
    internal const int TagLength = 16;
    internal const int MaxPlaintextLength = 1 << 14;
    internal const int MaxCiphertextLength = (1 << 14) + 256;

    private const int IvLength = 12;
    private const int ChaChaPolyKeyLength = 32;

    private readonly TlsCipherSuite cipherSuite;
    private readonly HashAlgorithmName hashAlgorithm;
    private readonly int hashLength;
    private readonly int keyLength;
    private byte[] trafficSecret = [];
    private byte[] key = [];
    private byte[] iv = [];

    internal TlsRecordKeys(TlsCipherSuite cipherSuite, ReadOnlySpan<byte> trafficSecret)
    {
        this.cipherSuite = cipherSuite;
        hashAlgorithm = TlsKeySchedule.HashAlgorithmOf(cipherSuite);
        hashLength = hashAlgorithm == HashAlgorithmName.SHA384 ? 48 : 32;
        keyLength = cipherSuite == TlsCipherSuite.Aes128GcmSha256 ? 16 : 32;
        if (trafficSecret.Length != hashLength)
        {
            throw new ArgumentException($"A traffic secret of this suite is {hashLength} bytes.", nameof(trafficSecret));
        }

        Derive(trafficSecret.ToArray());
    }

    /// <summary>The number of the next record this direction protects or unprotects; zero after the keys are set.</summary>
    internal ulong SequenceNumber { get; set; }

    /// <summary>The current traffic secret: the one the keys came from, or the last KeyUpdate's.</summary>
    internal byte[] TrafficSecret => trafficSecret;

    /// <summary>Derives the next traffic secret (RFC 8446 section 7.2, "traffic upd"), its key and IV, and restarts the sequence at zero.</summary>
    internal void Rekey() =>
        Derive(TlsKeySchedule.ExpandLabel(hashAlgorithm, trafficSecret, "traffic upd", [], hashLength));

    /// <summary>Encrypts an inner plaintext into ciphertext then tag and advances the sequence number.</summary>
    internal byte[] Seal(ReadOnlySpan<byte> header, ReadOnlySpan<byte> innerPlaintext)
    {
        var nonce = Nonce();
        var output = new byte[innerPlaintext.Length + TagLength];
        var ciphertext = output.AsSpan(0, innerPlaintext.Length);
        var tag = output.AsSpan(innerPlaintext.Length);
        if (cipherSuite == TlsCipherSuite.ChaCha20Poly1305Sha256)
        {
            ChaChaPolyEncrypt(nonce, header, innerPlaintext, ciphertext, tag);
        }
        else
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Encrypt(nonce, innerPlaintext, ciphertext, tag, header);
        }

        SequenceNumber++;
        return output;
    }

    /// <summary>Decrypts ciphertext then tag; advances the sequence number only when the tag is right.</summary>
    internal bool TryOpen(ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertextAndTag, out byte[] innerPlaintext)
    {
        var nonce = Nonce();
        var length = ciphertextAndTag.Length - TagLength;
        innerPlaintext = new byte[length];
        var opened = cipherSuite == TlsCipherSuite.ChaCha20Poly1305Sha256
            ? ChaChaPolyDecrypt(nonce, header, ciphertextAndTag[..length], ciphertextAndTag[length..], innerPlaintext)
            : AesGcmDecrypt(nonce, header, ciphertextAndTag[..length], ciphertextAndTag[length..], innerPlaintext);
        if (opened)
        {
            SequenceNumber++;
        }

        return opened;
    }

    private void Derive(byte[] secret)
    {
        trafficSecret = secret;
        key = TlsKeySchedule.ExpandLabel(hashAlgorithm, secret, "key", [], keyLength);
        iv = TlsKeySchedule.ExpandLabel(hashAlgorithm, secret, "iv", [], IvLength);
        SequenceNumber = 0;
    }

    /// <summary>The per-record nonce: the IV exclusive-ored with the 64-bit sequence number, left-padded with zeros (RFC 8446 section 5.3).</summary>
    private byte[] Nonce()
    {
        if (SequenceNumber == ulong.MaxValue)
        {
            throw new InvalidOperationException("The sequence number is exhausted; the keys must be updated (RFC 8446 section 5.3).");
        }

        var nonce = (byte[])iv.Clone();
        Span<byte> sequence = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(sequence, SequenceNumber);
        for (var i = 0; i < sequence.Length; i++)
        {
            nonce[IvLength - sequence.Length + i] ^= sequence[i];
        }

        return nonce;
    }

    private bool AesGcmDecrypt(
        byte[] nonce, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext)
    {
        using var aes = new AesGcm(key, TagLength);
        try
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext, header);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }

    private void ChaChaPolyEncrypt(
        byte[] nonce, ReadOnlySpan<byte> header, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag)
    {
        ChaCha20.ApplyKeyStream(key, nonce, 1, plaintext, ciphertext);
        ChaChaPolyTag(nonce, header, ciphertext, tag);
    }

    private bool ChaChaPolyDecrypt(
        byte[] nonce, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext)
    {
        Span<byte> expected = stackalloc byte[TagLength];
        ChaChaPolyTag(nonce, header, ciphertext, expected);
        if (!CryptographicOperations.FixedTimeEquals(expected, tag))
        {
            return false;
        }

        ChaCha20.ApplyKeyStream(key, nonce, 1, ciphertext, plaintext);
        return true;
    }

    /// <summary>The Poly1305 tag of RFC 8439 section 2.8: the one-time key is the first ChaCha20 block, the message is aad, ciphertext and their lengths.</summary>
    private void ChaChaPolyTag(byte[] nonce, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertext, Span<byte> tag)
    {
        Span<byte> block = stackalloc byte[ChaCha20.BlockSize];
        ChaCha20.ComputeBlock(key, nonce, 0, block);
        var aadPadded = (header.Length + 15) / 16 * 16;
        var ciphertextPadded = (ciphertext.Length + 15) / 16 * 16;
        var message = new byte[aadPadded + ciphertextPadded + 16];
        header.CopyTo(message);
        ciphertext.CopyTo(message.AsSpan(aadPadded));
        BinaryPrimitives.WriteUInt64LittleEndian(message.AsSpan(aadPadded + ciphertextPadded), (ulong)header.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(message.AsSpan(aadPadded + ciphertextPadded + 8), (ulong)ciphertext.Length);
        Poly1305.ComputeTag(block[..ChaChaPolyKeyLength], message, tag);
        CryptographicOperations.ZeroMemory(block);
    }
}
