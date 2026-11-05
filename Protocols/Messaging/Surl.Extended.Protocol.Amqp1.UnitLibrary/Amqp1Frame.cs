using System.Buffers.Binary;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// One AMQP 1.0 frame (Part 2 section 2.3): its type, its channel and its body - the bytes after
/// the header and any extended header. A frame with no body is an empty frame, which only keeps
/// the connection alive (section 2.4.5).
/// </summary>
/// <param name="Type">The frame type.</param>
/// <param name="Channel">The channel; ignored in a SASL frame.</param>
/// <param name="Body">The frame body: a performative, then for a <c>transfer</c> its payload; empty in an empty frame.</param>
public sealed record Amqp1Frame(Amqp1FrameType Type, ushort Channel, ReadOnlyMemory<byte> Body)
{
    /// <summary>The bytes of a frame header: size (4), DOFF (1), type (1) and channel (2).</summary>
    public const int HeaderBytes = 8;

    /// <summary>The DOFF this library writes: 2 four-byte words, the header with no extended header.</summary>
    public const byte DataOffsetWithoutExtendedHeader = 2;

    /// <summary>An empty AMQP frame on channel 0: the 8-byte heartbeat (section 2.4.5).</summary>
    public static readonly Amqp1Frame Empty = new(Amqp1FrameType.Amqp, 0, ReadOnlyMemory<byte>.Empty);

    /// <summary><see langword="true"/> when the frame has no body: an empty frame, a heartbeat.</summary>
    public bool IsEmpty => Body.IsEmpty;

    /// <summary>
    /// The frame that carries <paramref name="performative"/> on <paramref name="channel"/>, of the
    /// frame type the performative belongs to, with <paramref name="payload"/> after it.
    /// </summary>
    /// <param name="channel">The channel; 0 for a SASL performative.</param>
    /// <param name="performative">The performative.</param>
    /// <param name="payload">The bytes after the performative: a transfer's message bytes; empty otherwise.</param>
    /// <returns>The frame.</returns>
    public static Amqp1Frame Carrying(ushort channel, Amqp1Performative performative, ReadOnlyMemory<byte> payload = default)
    {
        ArgumentNullException.ThrowIfNull(performative);

        var encoder = new Amqp1Encoder();
        encoder.WriteValue(performative.ToDescribed());
        byte[] body = [.. encoder.Written.Span, .. payload.Span];
        return new(performative.FrameType, channel, body);
    }

    /// <summary>Encodes the frame: the header with DOFF 2, then the body.</summary>
    /// <returns>The bytes.</returns>
    public byte[] Encode()
    {
        var frame = new byte[HeaderBytes + Body.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)frame.Length);
        frame[4] = DataOffsetWithoutExtendedHeader;
        frame[5] = (byte)Type;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(6), Channel);
        Body.Span.CopyTo(frame.AsSpan(HeaderBytes));
        return frame;
    }
}
