namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The RDATA of a type read and written as opaque bytes (RFC 3597 section 5): every type
/// <see cref="DnsMessageReader"/> does not read field by field. Nothing in it is ever compressed
/// (RFC 3597 section 4).
/// </summary>
public sealed record DnsOpaqueData : DnsRecordData
{
    private readonly DnsRecordType type;
    private readonly byte[] bytes;

    /// <summary>Creates the RDATA of a record of <paramref name="type"/> holding <paramref name="bytes"/>.</summary>
    public DnsOpaqueData(DnsRecordType type, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        this.type = type;
        this.bytes = bytes.ToArray();
    }

    /// <summary>The RDATA's bytes.</summary>
    public ReadOnlyMemory<byte> Bytes => bytes;

    /// <inheritdoc/>
    public override DnsRecordType Type => type;

    /// <inheritdoc/>
    internal override void WriteTo(DnsMessageWriter writer) => writer.WriteBytes(bytes);
}
