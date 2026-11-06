namespace Surl.Dtls;

/// <summary>
/// Reads the records of one epoch from received datagrams, several to a datagram (RFC 6347
/// section 4.1.1), and drops every invalid one silently (section 4.1.2.7): a record of another
/// epoch, a replay, a record whose tag fails, one longer than 2^14 plaintext bytes, and a
/// truncated tail.
/// </summary>
/// <param name="epoch">The epoch whose records are accepted.</param>
/// <param name="protection">The epoch's protection, or <see langword="null"/> for plaintext records.</param>
public sealed class DtlsRecordReader(ushort epoch, DtlsRecordProtection? protection)
{
    private readonly DtlsReplayWindow replayWindow = new();

    /// <summary>The epoch whose records are accepted.</summary>
    public ushort Epoch { get; } = epoch;

    /// <summary>The epoch's protection, or <see langword="null"/> when its records are plaintext.</summary>
    internal DtlsRecordProtection? Protection => protection;

    /// <summary>
    /// Reads every record in <paramref name="datagram"/>.
    /// </summary>
    /// <param name="datagram">One whole received datagram.</param>
    /// <returns>The records accepted and the reasons the others were dropped.</returns>
    public DtlsDatagramRecords ReadDatagram(ReadOnlySpan<byte> datagram)
    {
        var records = new List<DtlsRecord>();
        var drops = new List<DtlsRecordDropReason>();

        while (!datagram.IsEmpty)
        {
            if (!DtlsRecordHeader.TryRead(datagram, out var header)
                || datagram.Length - DtlsRecordHeader.Size < header.Length)
            {
                drops.Add(DtlsRecordDropReason.Truncated);
                break;
            }

            var fragment = datagram.Slice(DtlsRecordHeader.Size, header.Length);
            datagram = datagram[(DtlsRecordHeader.Size + header.Length)..];

            var drop = TryAccept(header, fragment, out var record);
            if (drop is { } reason)
            {
                drops.Add(reason);
            }
            else
            {
                records.Add(record!);
            }
        }

        return new DtlsDatagramRecords(records, drops);
    }

    private DtlsRecordDropReason? TryAccept(DtlsRecordHeader header, ReadOnlySpan<byte> fragment, out DtlsRecord? record)
    {
        record = null;
        if (header.Epoch != Epoch)
        {
            return DtlsRecordDropReason.WrongEpoch;
        }

        if (!replayWindow.IsNew(header.SequenceNumber))
        {
            return DtlsRecordDropReason.Replayed;
        }

        if (!TryOpen(header, fragment, out var plaintext))
        {
            return DtlsRecordDropReason.FailedAuthentication;
        }

        if (plaintext.Length > DtlsRecordWriter.MaximumPlaintextLength)
        {
            return DtlsRecordDropReason.TooLong;
        }

        replayWindow.MarkAccepted(header.SequenceNumber);
        record = new DtlsRecord(header, plaintext);
        return null;
    }

    private bool TryOpen(DtlsRecordHeader header, ReadOnlySpan<byte> fragment, out byte[] plaintext)
    {
        if (protection is null)
        {
            plaintext = fragment.ToArray();
            return true;
        }

        return protection.TryOpen(header, fragment, out plaintext);
    }
}
