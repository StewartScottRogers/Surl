namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>The RDATA of MX (RFC 1035 section 3.3.9), its exchange written compressed.</summary>
/// <param name="Preference">PREFERENCE: lower is preferred.</param>
/// <param name="Exchange">EXCHANGE, the mail host.</param>
public sealed record DnsMailExchangeData(ushort Preference, DnsName Exchange) : DnsRecordData
{
    /// <inheritdoc/>
    public override DnsRecordType Type => DnsRecordType.MX;

    /// <inheritdoc/>
    internal override void WriteTo(DnsMessageWriter writer)
    {
        writer.WriteUInt16(Preference);
        writer.WriteName(Exchange, compress: true);
    }
}
