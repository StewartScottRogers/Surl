namespace Surl.Http2;

/// <summary>
/// The ten frame types RFC 9113 section 6 defines, by their type code (section 11.2). Any other
/// code is an unknown frame type, which is read and skipped (section 5.5).
/// </summary>
internal enum Http2FrameType : byte
{
    /// <summary><c>DATA</c> (section 6.1): request or response content.</summary>
    Data = 0x0,

    /// <summary><c>HEADERS</c> (section 6.2): opens a stream and carries a field block fragment.</summary>
    Headers = 0x1,

    /// <summary><c>PRIORITY</c> (section 6.3): the deprecated priority signal, parsed and ignored.</summary>
    Priority = 0x2,

    /// <summary><c>RST_STREAM</c> (section 6.4): ends one stream with an error code.</summary>
    RstStream = 0x3,

    /// <summary><c>SETTINGS</c> (section 6.5): connection parameters, or their acknowledgement.</summary>
    Settings = 0x4,

    /// <summary><c>PUSH_PROMISE</c> (section 6.6): a server push, which Surl never sends.</summary>
    PushPromise = 0x5,

    /// <summary><c>PING</c> (section 6.7): 8 opaque bytes, echoed back with <c>ACK</c>.</summary>
    Ping = 0x6,

    /// <summary><c>GOAWAY</c> (section 6.8): ends the connection, naming the last stream processed.</summary>
    GoAway = 0x7,

    /// <summary><c>WINDOW_UPDATE</c> (section 6.9): grows a flow-control window.</summary>
    WindowUpdate = 0x8,

    /// <summary><c>CONTINUATION</c> (section 6.10): the rest of a field block.</summary>
    Continuation = 0x9,
}
