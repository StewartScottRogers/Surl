namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The 4-bit RCODE a message header carries (RFC 1035 section 4.1.1, with RFC 2136's NOTAUTH).
/// An extended RCODE such as BADVERS (16) puts its upper eight bits in the OPT record instead
/// (<see cref="DnsOptRecord.ExtendedResponseCodeHigh"/>, RFC 6891 section 6.1.3).
/// </summary>
public enum DnsResponseCode : byte
{
    /// <summary>No error.</summary>
    NoError = 0,

    /// <summary>The server could not read the query.</summary>
    FormatError = 1,

    /// <summary>The server could not answer because of a problem of its own.</summary>
    ServerFailure = 2,

    /// <summary>The name does not exist.</summary>
    NonExistentDomain = 3,

    /// <summary>The kind of query is not implemented.</summary>
    NotImplemented = 4,

    /// <summary>The server refuses to answer.</summary>
    Refused = 5,

    /// <summary>The server is not authoritative for the zone (RFC 2136 section 2.2).</summary>
    NotAuthoritative = 9,
}
