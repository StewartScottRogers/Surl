using System.Text;
using System.Text.Unicode;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Reads a Socket.IO v5 packet from the text of one Engine.IO <c>message</c> packet, in the default
/// parser's encoding, and refuses a malformed one (SIO "Packet encoding"; ADR-0120, decisions 5, 6 and 7).
/// </summary>
/// <remarks>
/// A packet is malformed - a protocol error - when its type digit is outside <c>0</c> to <c>6</c>; when
/// a binary packet's attachment count is missing, below 1, above <see cref="MaxAttachments"/> or not
/// followed by <c>-</c>; when a namespace has no <c>,</c>, is over <see cref="MaxNamespaceBytes"/> bytes
/// or is not UTF-8; when an ack id is over <see cref="MaxAckIdDigits"/> digits, or an ACK or BINARY_ACK
/// has none; or when the payload breaks <see cref="SocketIoPayloadCheck"/>'s rules.
/// </remarks>
public static class SocketIoPacketReader
{
    /// <summary>The most attachments one binary packet may announce.</summary>
    public const int MaxAttachments = 64;

    /// <summary>The longest namespace, in UTF-8 bytes, its leading <c>/</c> included.</summary>
    public const int MaxNamespaceBytes = 128;

    /// <summary>The most decimal digits in an ack id.</summary>
    public const int MaxAckIdDigits = 15;

    private const int MaxAttachmentCountDigits = 2;

    /// <summary>Reads a packet of any type.</summary>
    /// <param name="text">The text of the Engine.IO <c>message</c> packet.</param>
    /// <param name="packet">The packet read, or <see langword="null"/> when it is malformed.</param>
    /// <returns><see langword="false"/> when the packet is malformed.</returns>
    public static bool TryRead(ReadOnlyMemory<byte> text, out SocketIoPacket? packet)
    {
        packet = null;
        var span = text.Span;
        if (span.IsEmpty || span[0] is < (byte)'0' or > (byte)'6')
        {
            return false;
        }

        var type = (SocketIoPacketType)(span[0] - '0');
        var position = 1;
        var attachments = 0;
        if (SocketIoPacket.CarriesAttachments(type) && !TryReadAttachmentCount(span, ref position, out attachments))
        {
            return false;
        }

        if (!TryReadNamespace(span, ref position, out var name) || !TryReadAckId(span, type, ref position, out var ackId))
        {
            return false;
        }

        var payload = text[position..];
        if (!SocketIoPayloadCheck.IsValid(type, payload.Span, attachments))
        {
            return false;
        }

        packet = new SocketIoPacket(type, name, attachments, ackId, payload);
        return true;
    }

    /// <summary>
    /// Reads a packet a client sent: as <see cref="TryRead"/>, and a CONNECT_ERROR, which only a server
    /// sends, is malformed too (ADR-0120, decision 5).
    /// </summary>
    /// <param name="text">The text of the Engine.IO <c>message</c> packet.</param>
    /// <param name="packet">The packet read, or <see langword="null"/> when it is malformed.</param>
    /// <returns><see langword="false"/> when the packet is malformed or is a CONNECT_ERROR.</returns>
    public static bool TryReadFromClient(ReadOnlyMemory<byte> text, out SocketIoPacket? packet)
    {
        if (TryRead(text, out packet) && packet!.Type != SocketIoPacketType.ConnectError)
        {
            return true;
        }

        packet = null;
        return false;
    }

    private static bool TryReadAttachmentCount(ReadOnlySpan<byte> span, ref int position, out int attachments)
    {
        attachments = 0;
        var digits = CountDigits(span[position..]);
        var dash = position + digits;
        if (digits is 0 or > MaxAttachmentCountDigits || dash >= span.Length || span[dash] != (byte)'-')
        {
            return false;
        }

        attachments = (int)ParseDigits(span.Slice(position, digits));
        position = dash + 1;
        return attachments is >= 1 and <= MaxAttachments;
    }

    private static bool TryReadNamespace(ReadOnlySpan<byte> span, ref int position, out string name)
    {
        name = SocketIoPacket.MainNamespace;
        if (position >= span.Length || span[position] != (byte)'/')
        {
            return true;
        }

        var comma = span[position..].IndexOf((byte)',');
        if (comma < 0 || comma > MaxNamespaceBytes || !Utf8.IsValid(span.Slice(position, comma)))
        {
            return false;
        }

        name = Encoding.UTF8.GetString(span.Slice(position, comma));
        position += comma + 1;
        return true;
    }

    private static bool TryReadAckId(ReadOnlySpan<byte> span, SocketIoPacketType type, ref int position, out long? ackId)
    {
        ackId = null;
        var digits = SocketIoPacket.MayCarryAckId(type) ? CountDigits(span[position..]) : 0;
        if (digits == 0)
        {
            return !SocketIoPacket.RequiresAckId(type);
        }

        if (digits > MaxAckIdDigits)
        {
            return false;
        }

        ackId = ParseDigits(span.Slice(position, digits));
        position += digits;
        return true;
    }

    private static int CountDigits(ReadOnlySpan<byte> span)
    {
        var end = span.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
        return end < 0 ? span.Length : end;
    }

    private static long ParseDigits(ReadOnlySpan<byte> digits)
    {
        long value = 0;
        foreach (var digit in digits)
        {
            value = (value * 10) + (digit - '0');
        }

        return value;
    }
}
