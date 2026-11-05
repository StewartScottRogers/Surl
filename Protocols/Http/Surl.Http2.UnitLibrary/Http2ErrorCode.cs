namespace Surl.Http2;

/// <summary>
/// The error codes of RFC 9113 section 7, carried by <c>RST_STREAM</c> and <c>GOAWAY</c>. A code
/// not listed here is kept as its number and given no special meaning (section 7).
/// </summary>
public enum Http2ErrorCode : uint
{
    /// <summary><c>NO_ERROR</c>: a graceful end, not an error.</summary>
    NoError = 0x0,

    /// <summary><c>PROTOCOL_ERROR</c>: a protocol error with no more specific code.</summary>
    ProtocolError = 0x1,

    /// <summary><c>INTERNAL_ERROR</c>: the endpoint failed unexpectedly.</summary>
    InternalError = 0x2,

    /// <summary><c>FLOW_CONTROL_ERROR</c>: the peer broke the flow-control protocol.</summary>
    FlowControlError = 0x3,

    /// <summary><c>SETTINGS_TIMEOUT</c>: a <c>SETTINGS</c> frame was not acknowledged in time.</summary>
    SettingsTimeout = 0x4,

    /// <summary><c>STREAM_CLOSED</c>: a frame arrived after the stream was half-closed.</summary>
    StreamClosed = 0x5,

    /// <summary><c>FRAME_SIZE_ERROR</c>: a frame had an invalid size.</summary>
    FrameSizeError = 0x6,

    /// <summary><c>REFUSED_STREAM</c>: the stream was refused before any processing; it may be retried.</summary>
    RefusedStream = 0x7,

    /// <summary><c>CANCEL</c>: the stream is no longer needed.</summary>
    Cancel = 0x8,

    /// <summary><c>COMPRESSION_ERROR</c>: the field compression context could not be kept.</summary>
    CompressionError = 0x9,

    /// <summary><c>CONNECT_ERROR</c>: a <c>CONNECT</c> tunnel was reset or closed abnormally.</summary>
    ConnectError = 0xA,

    /// <summary><c>ENHANCE_YOUR_CALM</c>: the peer is generating excessive load.</summary>
    EnhanceYourCalm = 0xB,

    /// <summary><c>INADEQUATE_SECURITY</c>: the transport's security is not enough.</summary>
    InadequateSecurity = 0xC,

    /// <summary><c>HTTP_1_1_REQUIRED</c>: the request must be repeated over HTTP/1.1.</summary>
    Http11Required = 0xD,
}
