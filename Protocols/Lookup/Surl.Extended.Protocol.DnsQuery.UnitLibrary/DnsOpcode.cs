namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A message header's OPCODE (RFC 1035 section 4.1.1, with the later assignments). Any 4-bit
/// value may be read; the named members are those ADR-0092 decision 2 answers by name.
/// </summary>
public enum DnsOpcode : byte
{
    /// <summary>A standard query.</summary>
    Query = 0,

    /// <summary>An inverse query, obsoleted by RFC 3425.</summary>
    InverseQuery = 1,

    /// <summary>A server status request.</summary>
    Status = 2,

    /// <summary>A zone change notification (RFC 1996).</summary>
    Notify = 4,

    /// <summary>A dynamic update (RFC 2136).</summary>
    Update = 5,
}
