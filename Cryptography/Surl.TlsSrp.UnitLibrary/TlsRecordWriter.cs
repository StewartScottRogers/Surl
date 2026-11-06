using System.Buffers.Binary;

namespace Surl.TlsSrp;

/// <summary>
/// Writes TLS 1.2 records to a stream (RFC 5246 section 6.2): a payload is cut into fragments of at
/// most 2^14 bytes, each in its own record with version <c>03 03</c>, written in plaintext until
/// <see cref="ChangeProtection"/> is called after the server's <c>ChangeCipherSpec</c>, then
/// through that protection.
/// </summary>
/// <param name="stream">The connection's byte stream.</param>
public sealed class TlsRecordWriter(Stream stream)
{
    private TlsCbcRecordProtection? protection;

    /// <summary>Whether records are written through a protection yet.</summary>
    public bool IsProtected => protection is not null;

    /// <summary>Writes every following record through <paramref name="writeProtection"/>.</summary>
    /// <param name="writeProtection">The server's write protection, its sequence number at zero.</param>
    public void ChangeProtection(TlsCbcRecordProtection writeProtection)
    {
        ArgumentNullException.ThrowIfNull(writeProtection);
        protection = writeProtection;
    }

    /// <summary>
    /// Writes <paramref name="payload"/> as one or more records of <paramref name="contentType"/>
    /// and flushes. An empty payload is one empty record.
    /// </summary>
    /// <param name="contentType">The records' content type.</param>
    /// <param name="payload">The bytes to send.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the records are flushed.</returns>
    public async Task WriteAsync(TlsContentType contentType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var offset = 0;
        do
        {
            var count = Math.Min(TlsRecordFormat.MaxPlaintextLength, payload.Length - offset);
            var record = Frame(contentType, payload.Span.Slice(offset, count));
            await stream.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            offset += count;
        }
        while (offset < payload.Length);

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private byte[] Frame(TlsContentType contentType, ReadOnlySpan<byte> plaintext)
    {
        var fragment = protection is null
            ? plaintext.ToArray()
            : protection.Protect(contentType, TlsRecordFormat.Tls12Version, plaintext);
        var record = new byte[TlsRecordFormat.HeaderLength + fragment.Length];
        record[0] = (byte)contentType;
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(1), TlsRecordFormat.Tls12Version);
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(3), (ushort)fragment.Length);
        fragment.CopyTo(record, TlsRecordFormat.HeaderLength);
        return record;
    }
}
