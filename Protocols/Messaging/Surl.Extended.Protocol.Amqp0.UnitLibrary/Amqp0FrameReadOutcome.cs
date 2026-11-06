namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// What <see cref="Amqp0FrameReader"/> found when it read the next frame.
/// </summary>
public enum Amqp0FrameReadOutcome
{
    /// <summary>A whole frame was read and ended with <c>0xCE</c>.</summary>
    FrameRead,

    /// <summary>The client closed the connection between frames.</summary>
    ConnectionClosed,

    /// <summary>The client closed the connection part of the way through a frame.</summary>
    ConnectionClosedMidFrame,

    /// <summary>The type octet is not 1, 2, 3 or 8; the payload was not read (501).</summary>
    UnknownFrameType,

    /// <summary>The frame, header and frame-end included, is above frame-max; the payload was not read (501).</summary>
    FrameTooLarge,

    /// <summary>A heartbeat frame on a channel other than 0 (section 4.2.7); the payload was not read (501).</summary>
    HeartbeatOffChannelZero,

    /// <summary>The octet after the payload is not <c>0xCE</c> (section 4.2.3; 501).</summary>
    BadFrameEnd,
}
