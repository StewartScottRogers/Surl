namespace Surl.Protocol.Http;

/// <summary>
/// What one call of <see cref="Http2FrameReader.ReadFrameAsync"/> found.
/// </summary>
internal enum Http2FrameReadOutcome
{
    /// <summary>A well-formed frame of a type RFC 9113 defines.</summary>
    FrameRead,

    /// <summary>A frame of an unknown type, whose payload was read and discarded (section 5.5).</summary>
    UnknownFrameSkipped,

    /// <summary>A frame broke RFC 9113 on its own; see the malformation.</summary>
    Malformed,

    /// <summary>The peer closed between frames.</summary>
    ConnectionClosed,

    /// <summary>The peer closed partway through a frame.</summary>
    ConnectionClosedMidFrame,
}
