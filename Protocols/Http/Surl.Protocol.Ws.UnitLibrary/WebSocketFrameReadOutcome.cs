namespace Surl.Protocol.Ws;

/// <summary>
/// What <see cref="WebSocketFrameReader.ReadFrameAsync"/> found. Every outcome but
/// <see cref="FrameRead"/> means there is no frame, and every one but <see cref="FrameRead"/>
/// and <see cref="ConnectionClosed"/> names a frame RFC 6455 makes invalid whatever the server
/// wants; which close code, if any, answers it is the caller's choice.
/// </summary>
internal enum WebSocketFrameReadOutcome
{
    /// <summary>A whole, valid frame was read.</summary>
    FrameRead,

    /// <summary>The peer closed the connection before the first byte of a frame.</summary>
    ConnectionClosed,

    /// <summary>The peer closed the connection partway through a frame.</summary>
    ConnectionClosedMidFrame,

    /// <summary>The opcode is one section 5.2 reserves: 3-7 or 0xB-0xF.</summary>
    ReservedOpcode,

    /// <summary>A close, ping or pong frame has <c>FIN</c> clear (section 5.5).</summary>
    FragmentedControlFrame,

    /// <summary>A close, ping or pong frame's payload is over 125 bytes (section 5.5).</summary>
    ControlFramePayloadTooLong,

    /// <summary>
    /// The payload length is not in its minimal encoding: a 16-bit length under 126, or a 64-bit
    /// length under 65536 (section 5.2).
    /// </summary>
    PayloadLengthNotMinimal,

    /// <summary>The most significant bit of a 64-bit payload length is set (section 5.2).</summary>
    PayloadLengthMostSignificantBitSet,

    /// <summary>The frame, header included, is over the caller's maximum; no payload byte was read.</summary>
    FrameTooLarge,

    /// <summary>A close frame's payload is exactly one byte, too short for a status code (section 5.5.1).</summary>
    ClosePayloadOneByte,

    /// <summary>A close frame's status code is not one section 7.4 allows on the wire.</summary>
    CloseCodeNotAllowed,

    /// <summary>A close frame's reason is not valid UTF-8 (sections 5.5.1 and 8.1).</summary>
    CloseReasonNotUtf8,
}
