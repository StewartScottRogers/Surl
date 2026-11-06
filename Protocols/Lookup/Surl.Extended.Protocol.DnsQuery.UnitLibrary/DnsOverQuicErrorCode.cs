namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The application error codes DNS over QUIC closes a connection or a stream with (RFC 9250
/// section 4.3).
/// </summary>
public enum DnsOverQuicErrorCode : ulong
{
    /// <summary><c>DOQ_NO_ERROR</c>: the connection or stream is closed with no error.</summary>
    NoError = 0x0,

    /// <summary><c>DOQ_INTERNAL_ERROR</c>: the server failed for a reason of its own.</summary>
    InternalError = 0x1,

    /// <summary><c>DOQ_PROTOCOL_ERROR</c>: the client broke a rule of RFC 9250.</summary>
    ProtocolError = 0x2,

    /// <summary><c>DOQ_REQUEST_CANCELLED</c>: a query was cancelled by the side that sent it.</summary>
    RequestCancelled = 0x3,

    /// <summary><c>DOQ_EXCESSIVE_LOAD</c>: the server is closing the connection for load.</summary>
    ExcessiveLoad = 0x4,
}
