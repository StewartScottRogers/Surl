using System.Buffers.Binary;
using Surl.Tls13;

namespace Surl.Tls13Stream;

/// <summary>
/// Reads whole TLS records (RFC 8446 section 5.1) from a transport, first from the bytes the caller
/// had already read, then from the transport itself.
/// </summary>
internal sealed class Tls13RecordReader(Stream transport, ReadOnlyMemory<byte> alreadyRead)
{
    internal const int HeaderLength = 5;
    internal const int MaximumFragmentLength = (1 << 14) + 256;

    private ReadOnlyMemory<byte> pending = alreadyRead;

    /// <summary>Reads one record, header included.</summary>
    /// <returns>The record, or null when the transport ended between two records.</returns>
    /// <exception cref="TlsAlertException"><c>record_overflow</c> for a fragment over 2^14 + 256 bytes.</exception>
    /// <exception cref="EndOfStreamException">The transport ended inside a record.</exception>
    internal async ValueTask<byte[]?> ReadRecordAsync(CancellationToken cancellationToken)
    {
        var header = new byte[HeaderLength];
        if (!await FillAsync(header, atBoundary: true, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3));
        if (length > MaximumFragmentLength)
        {
            throw new TlsAlertException(TlsAlert.RecordOverflow, "A record's fragment is at most 2^14 + 256 bytes.");
        }

        var record = new byte[HeaderLength + length];
        header.CopyTo(record, 0);
        await FillAsync(record.AsMemory(HeaderLength), atBoundary: false, cancellationToken).ConfigureAwait(false);
        return record;
    }

    private async ValueTask<bool> FillAsync(Memory<byte> buffer, bool atBoundary, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var count = TakePending(buffer[filled..]);
            if (count == 0)
            {
                count = await transport.ReadAsync(buffer[filled..], cancellationToken).ConfigureAwait(false);
            }

            if (count == 0)
            {
                return filled == 0 && atBoundary
                    ? false
                    : throw new EndOfStreamException("The connection ended inside a TLS record.");
            }

            filled += count;
        }

        return true;
    }

    private int TakePending(Memory<byte> buffer)
    {
        var count = Math.Min(buffer.Length, pending.Length);
        pending[..count].CopyTo(buffer);
        pending = pending[count..];
        return count;
    }
}
