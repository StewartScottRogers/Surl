namespace Surl.Quic;

/// <summary>
/// The transport error codes of RFC 9000 section 20.1, carried in a <c>CONNECTION_CLOSE</c>
/// frame of type <c>0x1c</c>. <c>CRYPTO_ERROR</c> is the range <c>0x0100</c> to <c>0x01ff</c>,
/// the TLS alert added to <see cref="CryptoErrorBase"/> (RFC 9001 section 4.8).
/// </summary>
public enum QuicTransportErrorCode : ulong
{
    /// <summary><c>NO_ERROR</c>: the connection closes without an error.</summary>
    NoError = 0x00,

    /// <summary><c>INTERNAL_ERROR</c>: the endpoint met an error it cannot go on from.</summary>
    InternalError = 0x01,

    /// <summary><c>CONNECTION_REFUSED</c>: the server refuses the connection.</summary>
    ConnectionRefused = 0x02,

    /// <summary><c>FLOW_CONTROL_ERROR</c>: the peer sent more data than it was allowed.</summary>
    FlowControlError = 0x03,

    /// <summary><c>STREAM_LIMIT_ERROR</c>: the peer opened more streams than it was allowed.</summary>
    StreamLimitError = 0x04,

    /// <summary><c>STREAM_STATE_ERROR</c>: a frame arrived for a stream in a state that forbids it.</summary>
    StreamStateError = 0x05,

    /// <summary><c>FINAL_SIZE_ERROR</c>: a stream's final size was broken.</summary>
    FinalSizeError = 0x06,

    /// <summary><c>FRAME_ENCODING_ERROR</c>: a frame is badly formatted.</summary>
    FrameEncodingError = 0x07,

    /// <summary><c>TRANSPORT_PARAMETER_ERROR</c>: the transport parameters are malformed or invalid.</summary>
    TransportParameterError = 0x08,

    /// <summary><c>CONNECTION_ID_LIMIT_ERROR</c>: the peer gave more connection IDs than it was allowed.</summary>
    ConnectionIdLimitError = 0x09,

    /// <summary><c>PROTOCOL_VIOLATION</c>: a protocol rule with no more specific code was broken.</summary>
    ProtocolViolation = 0x0a,

    /// <summary><c>INVALID_TOKEN</c>: a Retry token was invalid.</summary>
    InvalidToken = 0x0b,

    /// <summary><c>APPLICATION_ERROR</c>: the application closes the connection.</summary>
    ApplicationError = 0x0c,

    /// <summary><c>CRYPTO_BUFFER_EXCEEDED</c>: more <c>CRYPTO</c> data arrived than can be buffered.</summary>
    CryptoBufferExceeded = 0x0d,

    /// <summary><c>KEY_UPDATE_ERROR</c>: a key update was broken.</summary>
    KeyUpdateError = 0x0e,

    /// <summary><c>AEAD_LIMIT_REACHED</c>: the AEAD's confidentiality or integrity limit was reached.</summary>
    AeadLimitReached = 0x0f,

    /// <summary><c>NO_VIABLE_PATH</c>: no network path can carry the connection.</summary>
    NoViablePath = 0x10,

    /// <summary>The first <c>CRYPTO_ERROR</c> code; the TLS alert is added to it.</summary>
    CryptoErrorBase = 0x0100,
}
