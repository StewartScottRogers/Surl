namespace Surl.MessageBus;

/// <summary>
/// One topic's append-only log (ADR-0100, decision 3.3): records at contiguous offsets from 0,
/// the oldest dropped by retention so that <see cref="LogStartOffset"/> rises. Not thread-safe:
/// <see cref="MessageBus"/> uses it only under its lock.
/// </summary>
internal sealed class TopicLog
{
    // Dropped records stay at the front of the list until they are as many as the held ones,
    // then are removed together, so dropping the oldest costs O(1) amortised.
    private readonly List<BusLogRecord> records = [];
    private int firstHeldIndex;

    /// <summary>The offset of the oldest record still held.</summary>
    public long LogStartOffset { get; private set; }

    /// <summary>The offset the next record appended gets.</summary>
    public long HighWatermark => LogStartOffset + HeldCount;

    /// <summary>The bytes the held records count (<see cref="MessageBus.LogRecordBytes"/>).</summary>
    public long Bytes { get; private set; }

    private int HeldCount => records.Count - firstHeldIndex;

    /// <summary>
    /// How many of the oldest records must go, and the bytes they count, for a record of
    /// <paramref name="incomingBytes"/> to be appended within <paramref name="maxBytes"/>. Every
    /// held record may go; the incoming one is always kept, however large.
    /// </summary>
    public (int Count, long Bytes) OldestToDrop(long incomingBytes, long maxBytes)
    {
        var count = 0;
        var dropped = 0L;
        while (count < HeldCount && Bytes - dropped + incomingBytes > maxBytes)
        {
            dropped += MessageBus.LogRecordBytes(records[firstHeldIndex + count].Message);
            count++;
        }

        return (count, dropped);
    }

    /// <summary>Drops the <paramref name="count"/> oldest held records, which count
    /// <paramref name="bytes"/>.</summary>
    public void DropOldest(int count, long bytes)
    {
        firstHeldIndex += count;
        LogStartOffset += count;
        Bytes -= bytes;
        if (firstHeldIndex > 0 && firstHeldIndex >= HeldCount)
        {
            records.RemoveRange(0, firstHeldIndex);
            firstHeldIndex = 0;
        }
    }

    /// <summary>Appends <paramref name="message"/>, which counts <paramref name="bytes"/>, and
    /// returns the offset it got.</summary>
    public long Append(BusMessage message, long bytes)
    {
        var offset = HighWatermark;
        records.Add(new BusLogRecord(offset, message));
        Bytes += bytes;
        return offset;
    }

    /// <summary>
    /// The held records from <paramref name="offset"/> on, in order, as many as fit in
    /// <paramref name="maxBytes"/> but always at least the first, so a record larger than the
    /// limit can still be read (Kafka's Fetch does the same).
    /// </summary>
    public List<BusLogRecord> ReadFrom(long offset, long maxBytes)
    {
        var read = new List<BusLogRecord>();
        var readBytes = 0L;
        for (var index = firstHeldIndex + (int)(offset - LogStartOffset); index < records.Count; index++)
        {
            var record = records[index];
            readBytes += MessageBus.LogRecordBytes(record.Message);
            if (read.Count > 0 && readBytes > maxBytes)
            {
                break;
            }

            read.Add(record);
        }

        return read;
    }
}
