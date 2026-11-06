using System.Buffers.Binary;

namespace Surl.TlsSrp;

/// <summary>
/// Reads TLS 1.2 records from a stream (RFC 5246 section 6.2): plaintext until
/// <see cref="ChangeProtection"/> is called after the peer's <c>ChangeCipherSpec</c>, then through
/// that protection. It enforces the record length limits and refuses an unknown content type.
/// </summary>
/// <param name="stream">The connection's byte stream.</param>
public sealed class TlsRecordReader(Stream stream)
{
    private TlsCbcRecordProtection? protection;

    /// <summary>Whether records are read through a protection yet.</summary>
    public bool IsProtected => protection is not null;

    /// <summary>Reads every following record through <paramref name="readProtection"/>.</summary>
    /// <param name="readProtection">The peer's write protection, its sequence number at zero.</param>
    public void ChangeProtection(TlsCbcRecordProtection readProtection)
    {
        ArgumentNullException.ThrowIfNull(readProtection);
        protection = readProtection;
    }

    /// <summary>Reads one record.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record, or <see langword="null"/> when the stream ends between records.</returns>
    /// <exception cref="EndOfStreamException">The stream ends inside a record.</exception>
    /// <exception cref="TlsAlertException">
    /// <see cref="TlsAlertDescription.UnexpectedMessage"/> for an unknown content type,
    /// <see cref="TlsAlertDescription.RecordOverflow"/> for a record over its limit, and whatever
    /// <see cref="TlsCbcRecordProtection.Unprotect"/> refuses.
    /// </exception>
    public async Task<TlsRecord?> ReadAsync(CancellationToken cancellationToken)
    {
        var header = new byte[TlsRecordFormat.HeaderLength];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);
        if (read == 0)
        {
            return null;
        }

        if (read < header.Length)
        {
            throw new EndOfStreamException($"The stream ended {read} bytes into a record header.");
        }

        var contentType = ReadContentType(header[0]);
        var version = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1));
        var fragment = new byte[ReadLength(header)];
        await stream.ReadExactlyAsync(fragment, cancellationToken).ConfigureAwait(false);
        var plaintext = protection is null ? fragment : protection.Unprotect(contentType, version, fragment);
        return new TlsRecord(contentType, version, plaintext);
    }

    private static TlsContentType ReadContentType(byte value)
    {
        if (value is < (byte)TlsContentType.ChangeCipherSpec or > (byte)TlsContentType.ApplicationData)
        {
            throw new TlsAlertException(TlsAlertDescription.UnexpectedMessage, $"A record has the unknown content type {value}.");
        }

        return (TlsContentType)value;
    }

    private int ReadLength(byte[] header)
    {
        int length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3));
        var limit = protection is null ? TlsRecordFormat.MaxPlaintextLength : TlsRecordFormat.MaxCiphertextLength;
        if (length > limit)
        {
            throw new TlsAlertException(
                TlsAlertDescription.RecordOverflow, $"A record of {length} bytes is over the limit of {limit}.");
        }

        return length;
    }
}
