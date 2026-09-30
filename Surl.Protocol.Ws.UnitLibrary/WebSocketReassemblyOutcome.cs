namespace Surl.Protocol.Ws;

/// <summary>
/// What <see cref="WebSocketMessageReassembler.Accept"/> made of one frame. The last four name a
/// message RFC 6455 makes invalid; after one of them the caller stops handing frames over.
/// </summary>
internal enum WebSocketReassemblyOutcome
{
    /// <summary>The frame began or continued a message that is not finished yet.</summary>
    FragmentHeld,

    /// <summary>The frame finished a message, which the step carries.</summary>
    MessageComplete,

    /// <summary>The frame is a control frame, which the step carries back; it may arrive between fragments (section 5.4).</summary>
    ControlFrame,

    /// <summary>A continuation frame arrived with no message started (section 5.4).</summary>
    ContinuationWithoutMessage,

    /// <summary>A text or binary frame arrived while a message was still unfinished (section 5.4).</summary>
    DataFrameInsideMessage,

    /// <summary>The message's joined payload is over the caller's maximum.</summary>
    MessageTooLarge,

    /// <summary>A text message's joined payload is not valid UTF-8 (section 8.1).</summary>
    TextNotUtf8,
}
