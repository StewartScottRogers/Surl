namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The RDATA of SRV (RFC 2782). Its target is never written compressed: RFC 2782 forbids it, and
/// RFC 3597 section 4 forbids it in every type defined after RFC 1035.
/// </summary>
/// <param name="Priority">Priority: lower is tried first.</param>
/// <param name="Weight">Weight among targets of one priority.</param>
/// <param name="Port">The service's port on the target.</param>
/// <param name="Target">The target host.</param>
public sealed record DnsServiceLocationData(ushort Priority, ushort Weight, ushort Port, DnsName Target) : DnsRecordData
{
    /// <inheritdoc/>
    public override DnsRecordType Type => DnsRecordType.SRV;

    /// <inheritdoc/>
    internal override void WriteTo(DnsMessageWriter writer)
    {
        writer.WriteUInt16(Priority);
        writer.WriteUInt16(Weight);
        writer.WriteUInt16(Port);
        writer.WriteName(Target, compress: false);
    }
}
