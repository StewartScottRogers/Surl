namespace Surl.Protocol.Ws;

/// <summary>
/// The opcodes RFC 6455 section 5.2 defines. Opcodes 3-7 and 0xB-0xF are reserved; the frame
/// reader reports them as <see cref="WebSocketFrameReadOutcome.ReservedOpcode"/>, so no frame
/// ever carries one.
/// </summary>
internal enum WebSocketOpcode : byte
{
    /// <summary>A continuation of the message the previous data frame started.</summary>
    Continuation = 0x0,

    /// <summary>The first frame of a text message, whose payload is UTF-8.</summary>
    Text = 0x1,

    /// <summary>The first frame of a binary message.</summary>
    Binary = 0x2,

    /// <summary>A close control frame (section 5.5.1).</summary>
    Close = 0x8,

    /// <summary>A ping control frame (section 5.5.2).</summary>
    Ping = 0x9,

    /// <summary>A pong control frame (section 5.5.3).</summary>
    Pong = 0xA,
}
