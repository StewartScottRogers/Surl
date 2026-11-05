using System.Buffers.Binary;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Writes a <see cref="DnsResponse"/> as one TCP frame (ADR-0092 decision 4; RFC 1035 section
/// 4.2.2): the two-byte length, then the message, in one buffer so they go to the connection in
/// one write (RFC 7766 section 8).
/// </summary>
/// <remarks>
/// A TCP answer is never cut for size below <see cref="MaxMessageLength"/>, the most the length
/// prefix can carry. Only a message past it - which only ANY at a very large node can reach - is
/// cut as <see cref="DnsResponseWriter.WriteWithin"/> says, with TC set.
/// </remarks>
public static class DnsTcpResponseWriter
{
    /// <summary>The longest message a two-byte length prefix can carry.</summary>
    public const int MaxMessageLength = ushort.MaxValue;

    /// <summary>The length of the prefix before each message.</summary>
    public const int LengthPrefixLength = 2;

    /// <summary>Returns the length prefix and the message that answers with <paramref name="response"/>, as one buffer.</summary>
    public static byte[] Write(DnsResponse response)
    {
        var message = DnsResponseWriter.WriteWithin(response, MaxMessageLength);
        var frame = new byte[LengthPrefixLength + message.Length];

        BinaryPrimitives.WriteUInt16BigEndian(frame, (ushort)message.Length);
        message.CopyTo(frame, LengthPrefixLength);
        return frame;
    }
}
