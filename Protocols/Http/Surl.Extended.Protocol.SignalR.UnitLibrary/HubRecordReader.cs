using System.Buffers;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Cuts the bytes a connection receives into JSON hub-protocol records at each <c>0x1E</c>,
/// whichever transport messages carry them: a message may hold several records, and a record may
/// span messages (HubProtocol.md "Overview"; ADR-0119, decisions 3 and 7).
/// </summary>
/// <remarks>
/// A record's bytes before its <c>0x1E</c> are counted as they arrive, and a record that passes the
/// limit is refused before the rest of it is read.
/// </remarks>
/// <param name="maxRecordBytes">
/// The most bytes one record may hold before its <c>0x1E</c>: <c>--max-message</c>; 0 means no limit.
/// </param>
public sealed class HubRecordReader(long maxRecordBytes)
{
    private readonly ArrayBufferWriter<byte> pending = new();

    /// <summary>
    /// Takes the next bytes received and adds each record they complete to <paramref name="records"/>,
    /// in order, keeping the bytes of an unfinished record for the next call.
    /// </summary>
    /// <param name="received">The bytes received.</param>
    /// <param name="records">Where each completed record is added, without its <c>0x1E</c>.</param>
    /// <returns>
    /// <see langword="false"/> once a record passes the limit: the connection must be closed, and
    /// the records before it have been added.
    /// </returns>
    public bool TryAppend(ReadOnlySpan<byte> received, List<ReadOnlyMemory<byte>> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var rest = received;
        while (true)
        {
            var end = rest.IndexOf(HubMessageJsonWriter.RecordSeparator);
            var part = end < 0 ? rest : rest[..end];
            if (PassesLimit(part.Length))
            {
                return false;
            }

            pending.Write(part);
            if (end < 0)
            {
                return true;
            }

            records.Add(pending.WrittenSpan.ToArray());
            pending.Clear();
            rest = rest[(end + 1)..];
        }
    }

    private bool PassesLimit(int moreBytes) =>
        maxRecordBytes != 0 && pending.WrittenCount + (long)moreBytes > maxRecordBytes;
}
