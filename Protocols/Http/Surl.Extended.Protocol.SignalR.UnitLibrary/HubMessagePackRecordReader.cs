namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Cuts the bytes a connection receives into MessagePack hub-protocol messages by their VarInt
/// length prefixes, whichever WebSocket messages carry them: a message may hold several, and one may
/// span WebSocket messages (HubProtocol.md "Binary encoding"; ADR-0119, decisions 3 and 7).
/// </summary>
/// <remarks>
/// A prefix longer than five bytes, or naming a message past the limit, is refused as soon as it is
/// read, before the message's bytes arrive.
/// </remarks>
/// <param name="maxRecordBytes">The most bytes one message may hold: <c>--max-message</c>; 0 means no limit.</param>
public sealed class HubMessagePackRecordReader(long maxRecordBytes)
{
    private const int MaxPrefixBytes = 5;

    private byte[] pending = [];
    private int pendingCount;

    /// <summary>
    /// Takes the next bytes received and adds each message they complete to <paramref name="records"/>,
    /// in order, keeping the bytes of an unfinished one for the next call.
    /// </summary>
    /// <param name="received">The bytes received.</param>
    /// <param name="records">Where each completed message is added, without its length prefix.</param>
    /// <returns>
    /// <see langword="false"/> once a prefix is malformed or a message passes the limit: the connection
    /// must be closed, and the messages before it have been added.
    /// </returns>
    public bool TryAppend(ReadOnlySpan<byte> received, List<ReadOnlyMemory<byte>> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (pending.Length < pendingCount + received.Length)
        {
            Array.Resize(ref pending, pendingCount + received.Length);
        }

        received.CopyTo(pending.AsSpan(pendingCount));
        pendingCount += received.Length;

        var start = 0;
        var isWithinLimit = true;
        while (TryReadPrefix(start, out var length, out var prefixBytes, out isWithinLimit)
            && pendingCount - start - prefixBytes >= length)
        {
            records.Add(pending.AsSpan(start + prefixBytes, (int)length).ToArray());
            start += prefixBytes + (int)length;
        }

        Array.Copy(pending, start, pending, 0, pendingCount - start);
        pendingCount -= start;

        return isWithinLimit;
    }

    // False when the prefix is not all here yet; isWithinLimit is false when it is malformed or too large.
    private bool TryReadPrefix(int start, out long length, out int prefixBytes, out bool isWithinLimit)
    {
        length = 0;
        prefixBytes = 0;
        isWithinLimit = true;
        while (start + prefixBytes < pendingCount)
        {
            var next = pending[start + prefixBytes];
            length |= (long)(next & 0x7f) << (7 * prefixBytes);
            prefixBytes++;
            if ((next & 0x80) == 0)
            {
                isWithinLimit = maxRecordBytes == 0 || length <= maxRecordBytes;

                return isWithinLimit;
            }

            if (prefixBytes == MaxPrefixBytes)
            {
                isWithinLimit = false;

                return false;
            }
        }

        return false;
    }
}
