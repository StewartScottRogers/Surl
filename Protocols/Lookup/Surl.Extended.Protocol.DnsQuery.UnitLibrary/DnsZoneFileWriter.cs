using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Writes a zone back as master-file text (RFC 1035 section 5.1) that <see cref="DnsZoneFileReader"/> reads to the same
/// records: one record per line, every name absolute, a TTL and the class <c>IN</c> (the only class a zone holds) on every line, RDATA field by field for the
/// types ADR-0092 decision 2 serves and as RFC 3597 section 5's <c>\# &lt;length&gt; &lt;hex&gt;</c> for every other.
/// </summary>
internal static class DnsZoneFileWriter
{
    /// <summary>Returns <paramref name="zone"/>'s records as master-file text, in the order the zone holds them.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>The text; each line ends with a line feed.</returns>
    public static string Write(DnsZone zone)
    {
        var text = new StringBuilder();
        foreach (var record in zone.Records)
        {
            text.Append(NameText(record.Name)).Append(' ')
                .Append(record.TimeToLive.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append("IN").Append(' ')
                .Append(DnsPresentationReader.TypeText(record.Type)).Append(' ')
                .Append(DataText(record.Data)).Append('\n');
        }

        return text.ToString();
    }

    private static string DataText(DnsRecordData data) => data switch
    {
        DnsAddressData address => address.Address.ToString(),
        DnsDomainNameData domain => NameText(domain.Name),
        DnsMailExchangeData mail => $"{mail.Preference} {NameText(mail.Exchange)}",
        DnsServiceLocationData service => $"{service.Priority} {service.Weight} {service.Port} {NameText(service.Target)}",
        DnsStartOfAuthorityData authority => string.Join(
            ' ',
            NameText(authority.PrimaryNameServer),
            NameText(authority.ResponsibleMailbox),
            authority.Serial,
            authority.Refresh,
            authority.Retry,
            authority.Expire,
            authority.Minimum),
        DnsTextData text => string.Join(' ', text.Strings.Select(piece => QuotedText(piece.Span))),
        DnsCertificationAuthorityAuthorizationData authorization =>
            $"{authorization.Flags} {authorization.Tag} {QuotedText(authorization.Value.Span)}",
        _ => OpaqueText(DnsMessageWriter.WriteRecordData(data)),
    };

    private static string OpaqueText(byte[] bytes) =>
        bytes.Length == 0 ? "\\# 0" : $"\\# {bytes.Length} {Convert.ToHexString(bytes)}";

    private static string NameText(DnsName name) =>
        name.Labels.Count == 0
            ? "."
            : string.Concat(name.Labels.Select(label => EscapedText(label.Span, isLabel: true) + "."));

    private static string QuotedText(ReadOnlySpan<byte> bytes) => "\"" + EscapedText(bytes, isLabel: false) + "\"";

    private static string EscapedText(ReadOnlySpan<byte> bytes, bool isLabel)
    {
        var text = new StringBuilder();
        foreach (var value in bytes)
        {
            if (value is <= 0x20 or >= 0x7F)
            {
                text.Append('\\').Append(value.ToString("D3", CultureInfo.InvariantCulture));
            }
            else if (value is (byte)'\\' or (byte)'"' or (byte)';' or (byte)'(' or (byte)')' or (byte)'@' or (byte)'$'
                || (isLabel && value == (byte)'.'))
            {
                text.Append('\\').Append((char)value);
            }
            else
            {
                text.Append((char)value);
            }
        }

        return text.ToString();
    }
}
