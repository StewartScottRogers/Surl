using System.Buffers.Binary;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// One AMQP 0-9-1 frame (section 4.2.3): its type, its channel and its payload. On the wire it
/// is the type octet, the channel as a short, the payload size as a long, the payload, and the
/// frame-end octet <see cref="FrameEnd"/>.
/// </summary>
/// <param name="Type">The frame type.</param>
/// <param name="Channel">The channel; 0 for the connection and for heartbeats.</param>
/// <param name="Payload">The payload.</param>
public sealed record Amqp0Frame(Amqp0FrameType Type, ushort Channel, ReadOnlyMemory<byte> Payload)
{
    /// <summary>The frame-end octet every frame ends with: <c>0xCE</c>.</summary>
    public const byte FrameEnd = 0xCE;

    /// <summary>The bytes a frame adds to its payload: 7 of header and the frame-end octet.</summary>
    public const int OverheadBytes = HeaderBytes + 1;

    /// <summary>The bytes of a frame's header: type, channel and size.</summary>
    public const int HeaderBytes = 7;

    /// <summary>The heartbeat frame (section 4.2.7): type 8, channel 0, no payload.</summary>
    public static Amqp0Frame Heartbeat { get; } = new(Amqp0FrameType.Heartbeat, 0, ReadOnlyMemory<byte>.Empty);

    /// <summary>A method frame carrying <paramref name="method"/> on <paramref name="channel"/>.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="method">The method.</param>
    /// <returns>The frame.</returns>
    public static Amqp0Frame ForMethod(ushort channel, Amqp0MethodPayload method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return new(Amqp0FrameType.Method, channel, method.Encode());
    }

    /// <summary>A content header frame carrying <paramref name="header"/> on <paramref name="channel"/>.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="header">The content header.</param>
    /// <returns>The frame.</returns>
    public static Amqp0Frame ForContentHeader(ushort channel, Amqp0ContentHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        return new(Amqp0FrameType.ContentHeader, channel, header.Encode());
    }

    /// <summary>The frame's bytes as they go on the wire, frame-end included.</summary>
    /// <returns>The encoded frame.</returns>
    public byte[] Encode()
    {
        var bytes = new byte[OverheadBytes + Payload.Length];
        bytes[0] = (byte)Type;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(1), Channel);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(3), (uint)Payload.Length);
        Payload.Span.CopyTo(bytes.AsSpan(HeaderBytes));
        bytes[^1] = FrameEnd;
        return bytes;
    }
}
