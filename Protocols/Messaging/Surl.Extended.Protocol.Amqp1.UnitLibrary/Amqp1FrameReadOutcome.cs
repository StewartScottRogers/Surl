namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// What <see cref="Amqp1FrameReader"/> found when it read the next frame.
/// </summary>
public enum Amqp1FrameReadOutcome
{
    /// <summary>A whole frame was read.</summary>
    FrameRead,

    /// <summary>The client closed the connection between frames.</summary>
    ConnectionClosed,

    /// <summary>The client closed the connection part of the way through a frame.</summary>
    ConnectionClosedMidFrame,

    /// <summary>The size field is below 8, the header's own size; the rest was not read.</summary>
    FrameTooSmall,

    /// <summary>The size field is above the <c>max-frame-size</c> in force; the rest was not read.</summary>
    FrameTooLarge,

    /// <summary>DOFF is below 2, or points past the end of the frame; the rest was not read.</summary>
    BadDataOffset,

    /// <summary>The type octet is neither 0x00 (AMQP) nor 0x01 (SASL); the rest was not read.</summary>
    UnknownFrameType,
}
