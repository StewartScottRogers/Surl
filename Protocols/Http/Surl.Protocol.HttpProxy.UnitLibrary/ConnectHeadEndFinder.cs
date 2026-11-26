using System.Buffers.Binary;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Finds the empty line that ends a <c>CONNECT</c> head in the lines a client sends, whatever the
/// proxy's exchange-log redactor is reading them as (ADR-0182, amendment 23). A head starts at a line
/// that holds <c>CONNECT </c> and whose last word starts <c>HTTP/</c>, anywhere in the line because a
/// request body may run into it, and ends at the next empty line. Lines end with a line feed, with or
/// without a carriage return. No line is kept, so a line of any length is judged whole.
/// </summary>
internal sealed class ConnectHeadEndFinder
{
    private const ulong LastSixBytes = 0xFFFF_FFFF_FFFF;
    private const ulong CarriageReturnLineFeed = 0x0D0A;

    private static readonly ulong ConnectMethod = BinaryPrimitives.ReadUInt64BigEndian("CONNECT "u8);
    private static readonly ulong SpaceVersion = BinaryPrimitives.ReadUInt64BigEndian("\0\0 HTTP/"u8);

    private bool isInConnectHead;

    // The unfinished line: its last eight bytes (the newest lowest), its length counted no further
    // than three, and what has been seen in it.
    private ulong latest;
    private int length;
    private bool holdsConnect;
    private bool lastWordIsVersion;

    /// <summary>Takes the client's next bytes, which hold at most one line feed, as their last byte.</summary>
    /// <param name="bytes">The bytes, at least one.</param>
    /// <returns>True when they finish the empty line that ends a <c>CONNECT</c> head.</returns>
    public bool EndsConnectHead(ReadOnlySpan<byte> bytes)
    {
        foreach (var next in bytes)
        {
            Follow(next);
        }

        return bytes[^1] == (byte)'\n' && EndLine();
    }

    private void Follow(byte next)
    {
        latest = (latest << 8) | next;
        length = Math.Min(length + 1, 3);
        holdsConnect |= latest == ConnectMethod;
        lastWordIsVersion = next != (byte)' ' && (lastWordIsVersion || (latest & LastSixBytes) == SpaceVersion);
    }

    private bool EndLine()
    {
        var isEmpty = length == 1 || (length == 2 && latest == CarriageReturnLineFeed);
        var ends = isInConnectHead && isEmpty;
        isInConnectHead = !ends && (isInConnectHead || (holdsConnect && lastWordIsVersion));
        latest = 0;
        length = 0;
        holdsConnect = false;
        lastWordIsVersion = false;

        return ends;
    }
}
