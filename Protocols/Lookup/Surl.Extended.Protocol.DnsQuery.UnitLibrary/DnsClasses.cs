namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS CLASS values ADR-0092 serves (RFC 1035 sections 3.2.4 and 3.2.5). A record's class is
/// kept as a plain 16-bit number because the OPT pseudo-record uses the field for its payload
/// size instead (RFC 6891 section 6.1.2).
/// </summary>
public static class DnsClasses
{
    /// <summary>The Internet class, IN.</summary>
    public const ushort Internet = 1;

    /// <summary>Any class, a QCLASS only.</summary>
    public const ushort Any = 255;
}
