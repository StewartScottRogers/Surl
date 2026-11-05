namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A DNS TYPE or QTYPE (RFC 1035 sections 3.2.2 and 3.2.3). The named members are the types
/// ADR-0092 decision 2 reads and writes field by field, the EDNS(0) OPT pseudo-type and the
/// meta-types a question may carry; any other 16-bit value is a valid type too, carried as
/// <see cref="DnsOpaqueData"/> (RFC 3597).
/// </summary>
public enum DnsRecordType : ushort
{
    /// <summary>An IPv4 address (RFC 1035 section 3.4.1).</summary>
    A = 1,

    /// <summary>An authoritative name server (RFC 1035 section 3.3.11).</summary>
    NS = 2,

    /// <summary>The canonical name for an alias (RFC 1035 section 3.3.1).</summary>
    CNAME = 5,

    /// <summary>The start of a zone of authority (RFC 1035 section 3.3.13).</summary>
    SOA = 6,

    /// <summary>A domain name pointer (RFC 1035 section 3.3.12).</summary>
    PTR = 12,

    /// <summary>A mail exchange (RFC 1035 section 3.3.9).</summary>
    MX = 15,

    /// <summary>Text strings (RFC 1035 section 3.3.14).</summary>
    TXT = 16,

    /// <summary>An IPv6 address (RFC 3596 section 2.2).</summary>
    AAAA = 28,

    /// <summary>A service location (RFC 2782).</summary>
    SRV = 33,

    /// <summary>The EDNS(0) pseudo-record (RFC 6891 section 6).</summary>
    OPT = 41,

    /// <summary>An incremental zone transfer, a QTYPE only (RFC 1995).</summary>
    IXFR = 251,

    /// <summary>A whole zone transfer, a QTYPE only (RFC 5936).</summary>
    AXFR = 252,

    /// <summary>Every record at the name, a QTYPE only (RFC 1035 section 3.2.3).</summary>
    ANY = 255,

    /// <summary>A certification authority authorization (RFC 8659 section 4.1).</summary>
    CAA = 257,
}
