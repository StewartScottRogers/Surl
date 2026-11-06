namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The capsule types a WebTransport session's CONNECT stream carries (RFC 9297 section 3.2's
/// type field), with the codepoints of draft-ietf-webtrans-http3-16 section 9 and, for the two
/// HTTP/3 prohibits, draft-ietf-webtrans-http2 (ADR-0117 decision 4). Any other value is a
/// capsule type this codec gives no layout to.
/// </summary>
public enum WebTransportCapsuleType : ulong
{
    /// <summary><c>DATAGRAM</c> (RFC 9297 section 3.5): an HTTP datagram's payload.</summary>
    Datagram = 0x00,

    /// <summary><c>WT_CLOSE_SESSION</c> (WTH3 6): a 32-bit error code and a UTF-8 message.</summary>
    CloseSession = 0x2843,

    /// <summary><c>WT_DRAIN_SESSION</c> (WTH3 4.7): no value.</summary>
    DrainSession = 0x78ae,

    /// <summary><c>WT_MAX_DATA</c> (WTH3 5.6.2): the session's data limit.</summary>
    MaxData = 0x190b4d3d,

    /// <summary><c>WT_MAX_STREAM_DATA</c>: prohibited over HTTP/3 (WTH3 5.4).</summary>
    MaxStreamData = 0x190b4d3e,

    /// <summary><c>WT_MAX_STREAMS</c> for bidirectional streams (WTH3 5.6.3).</summary>
    MaxStreamsBidirectional = 0x190b4d3f,

    /// <summary><c>WT_MAX_STREAMS</c> for unidirectional streams (WTH3 5.6.3).</summary>
    MaxStreamsUnidirectional = 0x190b4d40,

    /// <summary><c>WT_DATA_BLOCKED</c> (WTH3 5.6.4): the data limit the sender is blocked at.</summary>
    DataBlocked = 0x190b4d41,

    /// <summary><c>WT_STREAM_DATA_BLOCKED</c>: prohibited over HTTP/3 (WTH3 5.4).</summary>
    StreamDataBlocked = 0x190b4d42,

    /// <summary><c>WT_STREAMS_BLOCKED</c> for bidirectional streams (WTH3 5.6.5).</summary>
    StreamsBlockedBidirectional = 0x190b4d43,

    /// <summary><c>WT_STREAMS_BLOCKED</c> for unidirectional streams (WTH3 5.6.5).</summary>
    StreamsBlockedUnidirectional = 0x190b4d44,
}
