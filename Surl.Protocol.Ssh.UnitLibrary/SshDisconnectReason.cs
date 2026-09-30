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

    /// <summary><c>SSH_DISCONNECT_PROTOCOL_VERSION_NOT_SUPPORTED</c>: a bad identification line.</summary>
    ProtocolVersionNotSupported = 8,

    /// <summary><c>SSH_DISCONNECT_BY_APPLICATION</c>: what the server cannot do yet.</summary>
    ByApplication = 11,
}
