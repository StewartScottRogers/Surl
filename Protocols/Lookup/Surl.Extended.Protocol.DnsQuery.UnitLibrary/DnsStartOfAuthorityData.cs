namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>The RDATA of SOA (RFC 1035 section 3.3.13), its two names written compressed.</summary>
/// <param name="PrimaryNameServer">MNAME.</param>
/// <param name="ResponsibleMailbox">RNAME.</param>
/// <param name="Serial">SERIAL.</param>
/// <param name="Refresh">REFRESH, in seconds.</param>
/// <param name="Retry">RETRY, in seconds.</param>
/// <param name="Expire">EXPIRE, in seconds.</param>
/// <param name="Minimum">MINIMUM, the negative-answer TTL of RFC 2308 section 4, in seconds.</param>
public sealed record DnsStartOfAuthorityData(
    DnsName PrimaryNameServer,
    DnsName ResponsibleMailbox,
    uint Serial,
    uint Refresh,
    uint Retry,
    uint Expire,
    uint Minimum) : DnsRecordData
{
    /// <inheritdoc/>
    public override DnsRecordType Type => DnsRecordType.SOA;

    /// <inheritdoc/>
    internal override void WriteTo(DnsMessageWriter writer)
    {
        writer.WriteName(PrimaryNameServer, compress: true);
        writer.WriteName(ResponsibleMailbox, compress: true);
        writer.WriteUInt32(Serial);
        writer.WriteUInt32(Refresh);
        writer.WriteUInt32(Retry);
        writer.WriteUInt32(Expire);
        writer.WriteUInt32(Minimum);
    }
}
