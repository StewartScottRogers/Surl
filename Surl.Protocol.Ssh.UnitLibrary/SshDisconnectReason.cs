namespace Surl.Protocol.Ssh;

/// <summary>
/// The <c>SSH_MSG_DISCONNECT</c> reason codes of RFC 4253 section 11.1 the server sends
/// (ADR-0051, decision 9).
/// </summary>
internal enum SshDisconnectReason : uint
{
    /// <summary><c>SSH_DISCONNECT_PROTOCOL_ERROR</c>: a packet or message the server refuses.</summary>
    ProtocolError = 2,

    /// <summary><c>SSH_DISCONNECT_KEY_EXCHANGE_FAILED</c>: no algorithm in common.</summary>
    KeyExchangeFailed = 3,

    /// <summary><c>SSH_DISCONNECT_MAC_ERROR</c>: a MAC or AEAD tag that does not verify.</summary>
    MacError = 5,

    /// <summary>
    /// <c>SSH_DISCONNECT_COMPRESSION_ERROR</c>: a payload that does not decompress, or
    /// decompresses past <c>--max-message</c>.
    /// </summary>
    CompressionError = 6,

    /// <summary>
    /// <c>SSH_DISCONNECT_SERVICE_NOT_AVAILABLE</c>: a <c>SERVICE_REQUEST</c> other than
    /// <c>ssh-userauth</c>, or a login for a service other than <c>ssh-connection</c>.
    /// </summary>
    ServiceNotAvailable = 7,

    /// <summary><c>SSH_DISCONNECT_PROTOCOL_VERSION_NOT_SUPPORTED</c>: a bad identification line.</summary>
    ProtocolVersionNotSupported = 8,

    /// <summary><c>SSH_DISCONNECT_BY_APPLICATION</c>: what the server cannot do yet.</summary>
    ByApplication = 11,

    /// <summary><c>SSH_DISCONNECT_NO_MORE_AUTH_METHODS_AVAILABLE</c>: the sixth refused login.</summary>
    NoMoreAuthMethodsAvailable = 14,
}
