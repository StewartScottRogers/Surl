namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// One EDNS(0) option in an OPT record's RDATA (RFC 6891 section 6.1.2): its code and its data,
/// written after a two-byte length.
/// </summary>
public sealed record DnsOption
{
    /// <summary>The Padding option's code (RFC 7830 section 3).</summary>
    public const ushort PaddingCode = 12;

    private readonly byte[] data;

    /// <summary>Creates the option <paramref name="code"/> holding <paramref name="data"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="data"/> is longer than 65535 bytes.</exception>
    public DnsOption(ushort code, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length > ushort.MaxValue)
        {
            throw new ArgumentException("An option holds at most 65535 bytes.", nameof(data));
        }

        Code = code;
        this.data = data.ToArray();
    }

    /// <summary>OPTION-CODE.</summary>
    public ushort Code { get; }

    /// <summary>OPTION-DATA.</summary>
    public ReadOnlyMemory<byte> Data => data;

    /// <summary>Writes the option's code, length and data.</summary>
    internal void WriteTo(DnsMessageWriter writer)
    {
        writer.WriteUInt16(Code);
        writer.WriteUInt16((ushort)data.Length);
        writer.WriteBytes(data);
    }
}
