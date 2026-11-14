using System.Buffers.Binary;

namespace Surl.Tls13;

/// <summary>
/// Unprotects one direction's TLS 1.3 records (RFC 8446 section 5.2): the mirror of
/// <see cref="TlsRecordProtector"/>. A record that cannot be accepted raises a
/// <see cref="TlsAlertException"/> carrying the alert to send. Not thread safe: records must be
/// unprotected in the order they arrive.
/// </summary>
public sealed class TlsRecordUnprotector
{
    private readonly TlsRecordKeys keys;

    /// <summary>Creates the unprotector for one traffic secret.</summary>
    /// <param name="cipherSuite">The negotiated cipher suite.</param>
    /// <param name="trafficSecret">The traffic secret of the peer's sending direction (one hash long).</param>
    /// <exception cref="ArgumentOutOfRangeException">The suite is not a TLS 1.3 cipher suite.</exception>
    /// <exception cref="ArgumentException">The secret is not one hash long.</exception>
    public TlsRecordUnprotector(TlsCipherSuite cipherSuite, ReadOnlySpan<byte> trafficSecret)
    {
        keys = new TlsRecordKeys(cipherSuite, trafficSecret);
    }

    /// <summary>The number of the next record to unprotect; zero after construction and after <see cref="UpdateKeys"/>.</summary>
    public ulong SequenceNumber => keys.SequenceNumber;

    /// <summary>The current traffic secret; the KeyUpdate secret after <see cref="UpdateKeys"/>.</summary>
    public byte[] TrafficSecret => keys.TrafficSecret;

    /// <summary>Unprotects one complete record.</summary>
    /// <param name="record">The record: the 5-byte header and the ciphertext, nothing more.</param>
    /// <returns>The real content type and the content, padding removed.</returns>
    /// <exception cref="TlsAlertException">
    /// <c>decode_error</c> for a header or length that does not fit the bytes, <c>unexpected_message</c> for an outer type
    /// other than 23 or an inner plaintext of zeros only, <c>record_overflow</c> for a ciphertext over 2^14 + 256 or content
    /// over 2^14 bytes, <c>bad_record_mac</c> when the tag does not verify.
    /// </exception>
    /// <exception cref="InvalidOperationException">The sequence number is exhausted.</exception>
    public TlsInnerPlaintext Unprotect(ReadOnlySpan<byte> record)
    {
        if (record.Length < TlsRecordKeys.HeaderLength)
        {
            throw new TlsAlertException(TlsAlert.DecodeError, "A record is at least its 5-byte header.");
        }

        if (record[0] != (byte)TlsContentType.ApplicationData)
        {
            throw new TlsAlertException(TlsAlert.UnexpectedMessage, "A protected record has outer content type 23.");
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(record[3..]);
        if (length > TlsRecordKeys.MaxCiphertextLength)
        {
            throw new TlsAlertException(TlsAlert.RecordOverflow, "A protected record is at most 2^14 + 256 bytes.");
        }

        if (length != record.Length - TlsRecordKeys.HeaderLength || length < TlsRecordKeys.TagLength + 1)
        {
            throw new TlsAlertException(TlsAlert.DecodeError, "The record length does not match the bytes, or leaves no room for a tag and a content type.");
        }

        if (!keys.TryOpen(record[..TlsRecordKeys.HeaderLength], record[TlsRecordKeys.HeaderLength..], out var inner))
        {
            throw new TlsAlertException(TlsAlert.BadRecordMac, "The record's authentication tag did not verify.");
        }

        var typeIndex = inner.AsSpan().LastIndexOfAnyExcept((byte)0);
        if (typeIndex < 0)
        {
            throw new TlsAlertException(TlsAlert.UnexpectedMessage, "The record holds only padding and no content type.");
        }

        if (typeIndex > TlsRecordKeys.MaxPlaintextLength)
        {
            throw new TlsAlertException(TlsAlert.RecordOverflow, "The record's content is over 2^14 bytes.");
        }

        return new TlsInnerPlaintext((TlsContentType)inner[typeIndex], inner[..typeIndex]);
    }

    /// <summary>Derives the next traffic secret, key and IV (RFC 8446 section 7.2) and restarts the sequence at zero: call it after receiving a KeyUpdate.</summary>
    public void UpdateKeys() => keys.Rekey();
}
