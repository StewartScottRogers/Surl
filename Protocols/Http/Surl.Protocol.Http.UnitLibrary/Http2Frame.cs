namespace Surl.Protocol.Http;

/// <summary>
/// One HTTP/2 frame of a type RFC 9113 section 6 defines, as a typed value. Each derived record
/// is one frame type and knows how to write its own payload; <see cref="Http2FrameEncoder"/>
/// adds the 9-byte header, and <see cref="Http2FrameParser"/> builds them from bytes.
/// </summary>
/// <param name="StreamId">The stream identifier, 0 for the connection.</param>
internal abstract record Http2Frame(int StreamId)
{
    /// <summary>The frame's type.</summary>
    public abstract Http2FrameType Type { get; }

    /// <summary>The flags byte the frame is written with.</summary>
    public abstract byte Flags { get; }

    /// <summary>The payload length the frame is written with, padding included.</summary>
    public abstract int PayloadLength { get; }

    /// <summary>
    /// Writes the payload into <paramref name="payload"/>, a new zeroed span of exactly
    /// <see cref="PayloadLength"/> bytes.
    /// </summary>
    /// <param name="payload">Where the payload goes.</param>
    public abstract void WritePayload(Span<byte> payload);
}
