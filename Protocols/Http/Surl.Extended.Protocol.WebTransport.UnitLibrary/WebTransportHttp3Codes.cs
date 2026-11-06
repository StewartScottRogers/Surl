namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The HTTP/3 codepoints WebTransport over HTTP/3 registers (draft-ietf-webtrans-http3-16 section 9)
/// that the server sends or requires, with RFC 9297's <c>SETTINGS_H3_DATAGRAM</c>.
/// </summary>
public static class WebTransportHttp3Codes
{
    /// <summary><c>SETTINGS_WT_ENABLED</c> (WTH3 3.1, 9.2): both ends send it = 1.</summary>
    public const ulong EnabledSetting = 0x2c7cf000;

    /// <summary><c>SETTINGS_H3_DATAGRAM</c> (RFC 9297 section 2.1.1): a client must send it = 1 to use WebTransport.</summary>
    public const ulong H3DatagramSetting = 0x33;

    /// <summary><c>WT_BUFFERED_STREAM_REJECTED</c> (WTH3 4.6): a stream for a session that never came, or was refused.</summary>
    public const ulong BufferedStreamRejected = 0x3994bd84;

    /// <summary><c>WT_SESSION_GONE</c> (WTH3 6): the stream's session has ended.</summary>
    public const ulong SessionGone = 0x170d7b68;

    /// <summary><c>WT_REQUIREMENTS_NOT_MET</c> (WTH3 3.1): the client's settings do not allow WebTransport.</summary>
    public const ulong RequirementsNotMet = 0x212c0d48;

    /// <summary><c>WT_FLOW_CONTROL_ERROR</c> (WTH3 5.6): the client broke a session's flow control.</summary>
    public const ulong FlowControlError = 0x045d4487;
}
