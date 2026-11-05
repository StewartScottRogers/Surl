namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The RDATA of the EDNS(0) OPT pseudo-record (RFC 6891 section 6.1.2): its options, in order.
/// The record's other fields are read through <see cref="DnsOptRecord"/>.
/// </summary>
public sealed record DnsOptData : DnsRecordData
{
    private readonly DnsOption[] options;

    /// <summary>Creates the RDATA holding <paramref name="options"/>, in order.</summary>
    public DnsOptData(IEnumerable<DnsOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.ToArray();
    }

    /// <summary>The options, in order.</summary>
    public IReadOnlyList<DnsOption> Options => options;

    /// <inheritdoc/>
    public override DnsRecordType Type => DnsRecordType.OPT;

    /// <inheritdoc/>
    internal override void WriteTo(DnsMessageWriter writer)
    {
        foreach (var option in options)
        {
            option.WriteTo(writer);
        }
    }
}
