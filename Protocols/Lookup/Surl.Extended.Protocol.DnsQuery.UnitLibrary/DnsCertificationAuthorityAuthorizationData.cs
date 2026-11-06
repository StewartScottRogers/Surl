using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The RDATA of CAA (RFC 8659 section 4.1): a flags byte, a tag of 1 to 15 ASCII letters and
/// digits after its length byte, and the value's bytes to the end of the RDATA.
/// </summary>
public sealed record DnsCertificationAuthorityAuthorizationData : DnsRecordData
{
    /// <summary>The longest tag (RFC 8659 section 4.1).</summary>
    public const int MaxTagLength = 15;

    private readonly byte[] value;

    /// <summary>Creates the RDATA with <paramref name="flags"/>, <paramref name="tag"/> and <paramref name="value"/>.</summary>
    /// <exception cref="ArgumentException">
    /// The tag is empty, longer than 15 characters, or holds a character other than an ASCII letter
    /// or digit.
    /// </exception>
    public DnsCertificationAuthorityAuthorizationData(byte flags, string tag, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(value);

        if (tag.Length is 0 or > MaxTagLength || !tag.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException("A CAA tag is 1 to 15 ASCII letters and digits.", nameof(tag));
        }

        Flags = flags;
        Tag = tag;
        this.value = value.ToArray();
    }

    /// <summary>The flags byte; bit 128 is Issuer Critical.</summary>
    public byte Flags { get; }

    /// <summary>The property tag, such as <c>issue</c>.</summary>
    public string Tag { get; }

    /// <summary>The property value.</summary>
    public ReadOnlyMemory<byte> Value => value;

    /// <inheritdoc/>
    public override DnsRecordType Type => DnsRecordType.CAA;

    /// <inheritdoc/>
    internal override void WriteTo(DnsMessageWriter writer)
    {
        writer.WriteUInt8(Flags);
        writer.WriteUInt8((byte)Tag.Length);
        writer.WriteBytes(Encoding.ASCII.GetBytes(Tag));
        writer.WriteBytes(value);
    }
}
