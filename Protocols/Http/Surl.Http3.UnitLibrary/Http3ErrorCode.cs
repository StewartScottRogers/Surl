namespace Surl.Http3;

/// <summary>
/// The HTTP/3 error codes (RFC 9114 section 8.1), and QPACK's (RFC 9204 section 6), that close an
/// HTTP/3 connection, reset a request stream or ask the client to stop sending on one.
/// </summary>
public enum Http3ErrorCode : ulong
{
    /// <summary><c>H3_NO_ERROR</c>: no error; the connection or stream simply ends.</summary>
    NoError = 0x100,

    /// <summary><c>H3_GENERAL_PROTOCOL_ERROR</c>: the client's transport parameters leave the server no room for its control and QPACK streams.</summary>
    GeneralProtocolError = 0x101,

    /// <summary><c>H3_INTERNAL_ERROR</c>: the server could not finish an answer it had started.</summary>
    InternalError = 0x102,

    /// <summary><c>H3_STREAM_CREATION_ERROR</c>: the client opened a stream it may not.</summary>
    StreamCreationError = 0x103,

    /// <summary><c>H3_CLOSED_CRITICAL_STREAM</c>: the client closed its control or a QPACK stream.</summary>
    ClosedCriticalStream = 0x104,

    /// <summary><c>H3_FRAME_UNEXPECTED</c>: a frame arrived where it is not allowed.</summary>
    FrameUnexpected = 0x105,

    /// <summary><c>H3_FRAME_ERROR</c>: a frame is malformed, or a stream ended inside one.</summary>
    FrameError = 0x106,

    /// <summary><c>H3_EXCESSIVE_LOAD</c>: a frame other than <c>DATA</c> is longer than <c>--max-message</c> (ADR-0082 decision 7).</summary>
    ExcessiveLoad = 0x107,

    /// <summary><c>H3_SETTINGS_ERROR</c>: a <c>SETTINGS</c> frame repeats a setting or carries an HTTP/2 one.</summary>
    SettingsError = 0x109,

    /// <summary><c>H3_MISSING_SETTINGS</c>: the client's control stream did not start with <c>SETTINGS</c>.</summary>
    MissingSettings = 0x10a,

    /// <summary><c>H3_REQUEST_REJECTED</c>: the server did not process a request opened after its <c>GOAWAY</c>.</summary>
    RequestRejected = 0x10b,

    /// <summary><c>H3_REQUEST_INCOMPLETE</c>: a request stream ended, or its head timed out, before its <c>HEADERS</c> was whole.</summary>
    RequestIncomplete = 0x10d,

    /// <summary><c>H3_MESSAGE_ERROR</c>: a request is malformed (RFC 9114 section 4.1.2).</summary>
    MessageError = 0x10e,

    /// <summary><c>QPACK_DECOMPRESSION_FAILED</c>: a field section could not be decoded.</summary>
    QpackDecompressionFailed = 0x200,

    /// <summary><c>QPACK_ENCODER_STREAM_ERROR</c>: the client's QPACK encoder stream broke a rule.</summary>
    QpackEncoderStreamError = 0x201,

    /// <summary><c>QPACK_DECODER_STREAM_ERROR</c>: the client's QPACK decoder stream broke a rule.</summary>
    QpackDecoderStreamError = 0x202,
}
