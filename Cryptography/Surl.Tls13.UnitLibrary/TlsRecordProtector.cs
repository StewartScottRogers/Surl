using System.Buffers.Binary;

namespace Surl.Tls13;

/// <summary>
/// Protects one direction's TLS 1.3 records (RFC 8446 section 5.2): the content, its real content
/// type and optional zero padding become the inner plaintext, sealed by the cipher suite's AEAD
/// under the key and IV of a traffic secret, with a nonce of the IV exclusive-ored with the record
/// sequence number. Not thread safe: records must be protected in the order they are sent.
/// </summary>
public sealed class TlsRecordProtector
{
    private readonly TlsRecordKeys keys;

    /// <summary>Creates the protector for one traffic secret.</summary>
    /// <param name="cipherSuite">The negotiated cipher suite.</param>
    /// <param name="trafficSecret">The traffic secret of this direction (one hash long).</param>
    /// <exception cref="ArgumentOutOfRangeException">The suite is not a TLS 1.3 cipher suite.</exception>
    /// <exception cref="ArgumentException">The secret is not one hash long.</exception>
    public TlsRecordProtector(TlsCipherSuite cipherSuite, ReadOnlySpan<byte> trafficSecret)
    {
        keys = new TlsRecordKeys(cipherSuite, trafficSecret);
    }

    /// <summary>The number of the next record to protect; zero after construction and after <see cref="UpdateKeys"/>.</summary>
    public ulong SequenceNumber => keys.SequenceNumber;

    /// <summary>The current traffic secret; the KeyUpdate secret after <see cref="UpdateKeys"/>.</summary>
    public byte[] TrafficSecret => keys.TrafficSecret;

    /// <summary>Protects one record.</summary>
    /// <param name="contentType">The real content type, hidden inside the encrypted record.</param>
    /// <param name="content">The content, at most 2^14 bytes.</param>
    /// <param name="paddingLength">The zero bytes to append inside the encryption (RFC 8446 section 5.4).</param>
    /// <returns>The complete record: a header with type 23 and version 0x0303, then ciphertext and tag.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The content exceeds 2^14 bytes, or the padding is negative or makes the record exceed 2^14 + 256 bytes of ciphertext.</exception>
    /// <exception cref="InvalidOperationException">The sequence number is exhausted.</exception>
    public byte[] Protect(TlsContentType contentType, ReadOnlySpan<byte> content, int paddingLength = 0)
    {
        if (content.Length > TlsRecordKeys.MaxPlaintextLength)
        {
            throw new ArgumentOutOfRangeException(nameof(content), content.Length, "A record carries at most 2^14 bytes.");
        }

        if (paddingLength < 0 || content.Length + 1L + paddingLength + TlsRecordKeys.TagLength > TlsRecordKeys.MaxCiphertextLength)
        {
            throw new ArgumentOutOfRangeException(nameof(paddingLength), paddingLength, "The padding must keep the ciphertext within 2^14 + 256 bytes.");
        }

        var innerLength = content.Length + 1 + paddingLength;
        var inner = new byte[innerLength];
        content.CopyTo(inner);
        inner[content.Length] = (byte)contentType;
        var header = new byte[TlsRecordKeys.HeaderLength];
        header[0] = (byte)TlsContentType.ApplicationData;
        header[1] = 0x03;
        header[2] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(3), (ushort)(innerLength + TlsRecordKeys.TagLength));
        var sealedBytes = keys.Seal(header, inner);
        var record = new byte[header.Length + sealedBytes.Length];
        header.CopyTo(record, 0);
        sealedBytes.CopyTo(record, header.Length);
        return record;
    }

    /// <summary>Derives the next traffic secret, key and IV (RFC 8446 section 7.2) and restarts the sequence at zero: call it after sending a KeyUpdate.</summary>
    public void UpdateKeys() => keys.Rekey();
}
