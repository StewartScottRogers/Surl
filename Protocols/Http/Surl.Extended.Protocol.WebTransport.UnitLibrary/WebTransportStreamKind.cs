namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The value that starts a WebTransport data stream, as a QUIC variable-length integer, before
/// its session ID (draft-ietf-webtrans-http3-16 sections 4.2 and 4.3).
/// </summary>
public enum WebTransportStreamKind : ulong
{
    /// <summary>The signal <c>0x41</c> that starts a client bidirectional stream (WTH3 4.3).</summary>
    Bidirectional = 0x41,

    /// <summary>The stream type <c>0x54</c> that starts a unidirectional stream (WTH3 4.2).</summary>
    Unidirectional = 0x54,
}
