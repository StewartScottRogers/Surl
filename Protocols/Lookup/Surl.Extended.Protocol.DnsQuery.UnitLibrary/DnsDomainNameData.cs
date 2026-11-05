namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The RDATA of NS (RFC 1035 section 3.3.11), CNAME (3.3.1) and PTR (3.3.12): one domain name,
/// written compressed.
/// </summary>
public sealed record DnsDomainNameData : DnsRecordData
{
    private readonly DnsRecordType type;

    /// <summary>Creates the RDATA of a record of <paramref name="type"/> naming <paramref name="name"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not NS, CNAME or PTR.</exception>
    public DnsDomainNameData(DnsRecordType type, DnsName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (type is not (DnsRecordType.NS or DnsRecordType.CNAME or DnsRecordType.PTR))
        {
            throw new ArgumentException("The RDATA of NS, CNAME and PTR is one name.", nameof(type));
        }

        this.type = type;
        Name = name;
    }

    /// <summary>The name: the name server, the canonical name or the pointer's target.</summary>
    public DnsName Name { get; }

    /// <inheritdoc/>
    public override DnsRecordType Type => type;

    /// <inheritdoc/>
    internal override void WriteTo(DnsMessageWriter writer) => writer.WriteName(Name, compress: true);
}
