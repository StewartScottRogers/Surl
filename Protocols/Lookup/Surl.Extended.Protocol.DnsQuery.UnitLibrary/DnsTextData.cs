namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The RDATA of TXT (RFC 1035 section 3.3.14): one or more character-strings, each a length byte
/// and up to 255 bytes.
/// </summary>
public sealed record DnsTextData : DnsRecordData
{
    /// <summary>The longest character-string, in bytes (RFC 1035 section 3.3).</summary>
    public const int MaxStringLength = 255;

    private readonly byte[][] strings;

    /// <summary>Creates the RDATA holding <paramref name="strings"/>, in order.</summary>
    /// <exception cref="ArgumentException">
    /// There is no string, or a string is longer than 255 bytes.
    /// </exception>
    public DnsTextData(IEnumerable<byte[]> strings)
    {
        ArgumentNullException.ThrowIfNull(strings);

        this.strings = strings.Select(text => text.ToArray()).ToArray();

        if (this.strings.Length == 0 || this.strings.Any(text => text.Length > MaxStringLength))
        {
            throw new ArgumentException("TXT holds one or more strings of up to 255 bytes.", nameof(strings));
        }
    }

    /// <summary>The character-strings, in order.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> Strings =>
        strings.Select(text => new ReadOnlyMemory<byte>(text)).ToArray();

    /// <inheritdoc/>
    public override DnsRecordType Type => DnsRecordType.TXT;

    /// <inheritdoc/>
    internal override void WriteTo(DnsMessageWriter writer)
    {
        foreach (var text in strings)
        {
            writer.WriteUInt8((byte)text.Length);
            writer.WriteBytes(text);
        }
    }
}
