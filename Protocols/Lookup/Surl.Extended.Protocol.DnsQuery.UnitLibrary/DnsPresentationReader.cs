using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Reads the presentation forms of a master file (RFC 1035 section 5.1): names, with <c>@</c>,
/// relative names and the <c>\X</c> and <c>\DDD</c> escapes; character-strings; TTLs; types,
/// by mnemonic or as RFC 3597's <c>TYPE&lt;n&gt;</c>; and RDATA, field by field for the types
/// ADR-0092 decision 2 serves or as RFC 3597 section 5's <c>\# &lt;length&gt; &lt;hex&gt;</c>.
/// Every fault is a <see cref="DnsZoneFileException"/> without a line; the zone reader adds it.
/// </summary>
internal static class DnsPresentationReader
{
    /// <summary>The largest TTL (RFC 2181 section 8).</summary>
    public const uint MaxTimeToLive = int.MaxValue;

    private static readonly HashSet<string> ClassMnemonics = new(["IN", "CH", "HS", "CS", "NONE"], StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, DnsRecordType> TypesByMnemonic = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = DnsRecordType.A,
        ["NS"] = DnsRecordType.NS,
        ["CNAME"] = DnsRecordType.CNAME,
        ["SOA"] = DnsRecordType.SOA,
        ["PTR"] = DnsRecordType.PTR,
        ["MX"] = DnsRecordType.MX,
        ["TXT"] = DnsRecordType.TXT,
        ["AAAA"] = DnsRecordType.AAAA,
        ["SRV"] = DnsRecordType.SRV,
        ["OPT"] = DnsRecordType.OPT,
        ["IXFR"] = DnsRecordType.IXFR,
        ["AXFR"] = DnsRecordType.AXFR,
        ["ANY"] = DnsRecordType.ANY,
        ["CAA"] = DnsRecordType.CAA,
    };

    private static readonly Dictionary<DnsRecordType, Func<IReadOnlyList<DnsMasterFileToken>, DnsName, DnsRecordData>> FieldReaders = new()
    {
        [DnsRecordType.A] = (tokens, _) => new DnsAddressData(ReadIPv4(Single(tokens))),
        [DnsRecordType.NS] = (tokens, origin) => new DnsDomainNameData(DnsRecordType.NS, ReadName(Single(tokens), origin)),
        [DnsRecordType.CNAME] = (tokens, origin) => new DnsDomainNameData(DnsRecordType.CNAME, ReadName(Single(tokens), origin)),
        [DnsRecordType.SOA] = ReadStartOfAuthority,
        [DnsRecordType.PTR] = (tokens, origin) => new DnsDomainNameData(DnsRecordType.PTR, ReadName(Single(tokens), origin)),
        [DnsRecordType.MX] = (tokens, origin) => new DnsMailExchangeData(
            ReadUInt16(Exactly(tokens, 2)[0]), ReadName(tokens[1], origin)),
        [DnsRecordType.TXT] = (tokens, _) => new DnsTextData(tokens.Select(ReadCharacterString)),
        [DnsRecordType.AAAA] = (tokens, _) => new DnsAddressData(ReadIPv6(Single(tokens))),
        [DnsRecordType.SRV] = (tokens, origin) => new DnsServiceLocationData(
            ReadUInt16(Exactly(tokens, 4)[0]), ReadUInt16(tokens[1]), ReadUInt16(tokens[2]), ReadName(tokens[3], origin)),
        [DnsRecordType.CAA] = (tokens, _) => new DnsCertificationAuthorityAuthorizationData(
            ReadUInt8(Exactly(tokens, 3)[0]), Plain(tokens[1]), ReadCharacterString(tokens[2])),
    };

    /// <summary>
    /// Returns the name <paramref name="token"/> writes: <c>@</c> is <paramref name="origin"/>, a
    /// name ending in an unescaped dot is absolute, and any other is completed with the origin.
    /// </summary>
    public static DnsName ReadName(DnsMasterFileToken token, DnsName origin)
    {
        var text = Plain(token);

        if (text == "@")
        {
            return origin;
        }

        if (text == ".")
        {
            return DnsName.Root;
        }

        try
        {
            var (labels, isAbsolute) = ReadLabels(text);
            var name = new DnsName(labels);
            return isAbsolute ? name : name.Append(origin);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw Fault($"a bad name {text}");
        }
    }

    /// <summary>Returns the TTL <paramref name="token"/> writes, decimal seconds up to 2147483647.</summary>
    public static uint ReadTimeToLive(DnsMasterFileToken token)
    {
        var text = Plain(token);

        if (!IsDecimal(text))
        {
            throw Fault($"a bad TTL {text}");
        }

        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var timeToLive) || timeToLive > MaxTimeToLive)
        {
            throw Fault("a TTL past 2147483647");
        }

        return timeToLive;
    }

    /// <summary>Whether <paramref name="token"/> is unquoted and all decimal digits, as a TTL is.</summary>
    public static bool IsTimeToLive(DnsMasterFileToken token) => !token.IsQuoted && IsDecimal(token.Text);

    /// <summary>
    /// Whether <paramref name="token"/> names a class: <c>IN</c>, <c>CH</c>, <c>HS</c>,
    /// <c>CS</c>, <c>NONE</c> or RFC 3597's <c>CLASS&lt;n&gt;</c>.
    /// </summary>
    public static bool IsClass(DnsMasterFileToken token) =>
        !token.IsQuoted && (ClassMnemonics.Contains(token.Text) || IsNumberedClass(token.Text));

    private static bool IsNumberedClass(string text) =>
        text.StartsWith("CLASS", StringComparison.OrdinalIgnoreCase) && IsDecimal(text[5..]);

    /// <summary>Whether the class <paramref name="token"/> names is IN, which <c>CLASS1</c> also writes.</summary>
    public static bool IsInternetClass(DnsMasterFileToken token) =>
        token.Text.Equals("IN", StringComparison.OrdinalIgnoreCase) || token.Text.Equals("CLASS1", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the type <paramref name="token"/> names, by mnemonic or as <c>TYPE&lt;n&gt;</c>,
    /// refusing a meta-type, which no zone record can be.
    /// </summary>
    public static DnsRecordType ReadType(DnsMasterFileToken token)
    {
        var text = Plain(token);
        var type = TypesByMnemonic.TryGetValue(text, out var named) ? named : ReadNumberedType(text);

        if (type is DnsRecordType.OPT or DnsRecordType.IXFR or DnsRecordType.AXFR or DnsRecordType.ANY)
        {
            throw Fault($"a meta-type {TypeText(type)} as a record");
        }

        return type;
    }

    /// <summary>Returns the mnemonic of <paramref name="type"/> when it has one here, or <c>TYPE&lt;n&gt;</c>.</summary>
    public static string TypeText(DnsRecordType type) =>
        TypesByMnemonic.FirstOrDefault(pair => pair.Value == type).Key ?? $"TYPE{(ushort)type}";

    /// <summary>
    /// Returns the RDATA of a <paramref name="type"/> record written as <paramref name="tokens"/>,
    /// names in it completed with <paramref name="origin"/>.
    /// </summary>
    public static DnsRecordData ReadRecordData(DnsRecordType type, IReadOnlyList<DnsMasterFileToken> tokens, DnsName origin)
    {
        try
        {
            return tokens is [{ IsQuoted: false, Text: @"\#" }, ..]
                ? ReadGenericRecordData(type, tokens)
                : ReadFields(type, tokens, origin);
        }
        catch (Exception exception) when (exception is DnsZoneFileException or ArgumentException or FormatException
            or OverflowException or DnsReadFaultException)
        {
            throw Fault($"bad RDATA for {TypeText(type)}");
        }
    }

    /// <summary>
    /// Returns the bytes of the character-string <paramref name="token"/> writes: each character
    /// as UTF-8, <c>\DDD</c> as the byte of that decimal value, and <c>\X</c> as X.
    /// </summary>
    public static byte[] ReadCharacterString(DnsMasterFileToken token)
    {
        var bytes = new List<byte>();
        var text = token.Text;

        for (var index = 0; index < text.Length;)
        {
            index = text[index] == '\\'
                ? AppendEscape(text, index + 1, bytes)
                : AppendCharacter(text, index, bytes);
        }

        return [.. bytes];
    }

    private static DnsRecordData ReadFields(DnsRecordType type, IReadOnlyList<DnsMasterFileToken> tokens, DnsName origin) =>
        FieldReaders.TryGetValue(type, out var reader)
            ? reader(tokens, origin)
            : throw new FormatException("A type without a presentation form here is written in the \\# form.");

    private static DnsRecordData ReadGenericRecordData(DnsRecordType type, IReadOnlyList<DnsMasterFileToken> tokens)
    {
        var length = ReadUInt16(tokens[1]);
        var bytes = Convert.FromHexString(string.Concat(tokens.Skip(2).Select(Plain)));

        if (bytes.Length != length)
        {
            throw Fault("a \\# length that is not the data's");
        }

        return FieldReaders.ContainsKey(type)
            ? DnsMessageReader.ReadRecordData(bytes, type)
            : new DnsOpaqueData(type, bytes);
    }

    private static DnsStartOfAuthorityData ReadStartOfAuthority(IReadOnlyList<DnsMasterFileToken> tokens, DnsName origin) => new(
        ReadName(Exactly(tokens, 7)[0], origin),
        ReadName(tokens[1], origin),
        ReadUInt32(tokens[2]),
        ReadUInt32(tokens[3]),
        ReadUInt32(tokens[4]),
        ReadUInt32(tokens[5]),
        ReadUInt32(tokens[6]));

    private static (List<byte[]> Labels, bool IsAbsolute) ReadLabels(string text)
    {
        var labels = new List<byte[]>();
        var label = new List<byte>();
        var index = 0;

        while (index < text.Length)
        {
            if (text[index] == '.')
            {
                labels.Add(EndLabel(label));
                index++;
                continue;
            }

            index = AppendNameCharacter(text, index, label);
        }

        var isAbsolute = text.EndsWith('.') && labels.Count > 0 && label.Count == 0;
        if (!isAbsolute)
        {
            labels.Add(EndLabel(label));
        }

        return (labels, isAbsolute);
    }

    private static byte[] EndLabel(List<byte> label)
    {
        byte[] bytes = [.. label];
        label.Clear();
        return bytes;
    }

    private static int AppendNameCharacter(string text, int index, List<byte> label)
    {
        var isEscape = text[index] == '\\';
        var characterIndex = isEscape ? index + 1 : index;

        if (characterIndex < text.Length && !char.IsAscii(text[characterIndex]))
        {
            throw new ArgumentException("A name is written in ASCII.", nameof(text));
        }

        return isEscape ? AppendEscape(text, characterIndex, label) : AppendCharacter(text, index, label);
    }

    private static int AppendEscape(string text, int index, List<byte> bytes)
    {
        if (index >= text.Length)
        {
            throw new FormatException("A backslash escapes nothing.");
        }

        if (!char.IsAsciiDigit(text[index]))
        {
            return AppendCharacter(text, index, bytes);
        }

        var digits = index + 3 <= text.Length ? text.Substring(index, 3) : string.Empty;
        if (!IsDecimal(digits) || !byte.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException("\\DDD is three decimal digits naming 0 to 255.");
        }

        bytes.Add(value);
        return index + 3;
    }

    private static int AppendCharacter(string text, int index, List<byte> bytes)
    {
        Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out var consumed);
        Span<byte> buffer = stackalloc byte[4];
        var written = rune.EncodeToUtf8(buffer);
        bytes.AddRange(buffer[..written]);
        return index + consumed;
    }

    private static DnsRecordType ReadNumberedType(string text)
    {
        if (text.StartsWith("TYPE", StringComparison.OrdinalIgnoreCase)
            && IsDecimal(text[4..])
            && ushort.TryParse(text[4..], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return (DnsRecordType)number;
        }

        throw Fault($"an unknown type {text}");
    }

    private static IPAddress ReadIPv4(DnsMasterFileToken token)
    {
        var parts = Plain(token).Split('.');

        if (parts.Length != 4 || !parts.All(part => part.Length <= 3 && IsDecimal(part)))
        {
            throw new FormatException("An A record is a dotted quad.");
        }

        return new IPAddress(parts.Select(part => byte.Parse(part, NumberStyles.None, CultureInfo.InvariantCulture)).ToArray());
    }

    private static IPAddress ReadIPv6(DnsMasterFileToken token)
    {
        var text = Plain(token);

        if (!text.Contains(':') || text.Contains('%')
            || !IPAddress.TryParse(text, out var address) || address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            throw new FormatException("An AAAA record is an IPv6 address.");
        }

        return address;
    }

    private static byte ReadUInt8(DnsMasterFileToken token) =>
        byte.Parse(Decimal(token), NumberStyles.None, CultureInfo.InvariantCulture);

    private static ushort ReadUInt16(DnsMasterFileToken token) =>
        ushort.Parse(Decimal(token), NumberStyles.None, CultureInfo.InvariantCulture);

    private static uint ReadUInt32(DnsMasterFileToken token) =>
        uint.Parse(Decimal(token), NumberStyles.None, CultureInfo.InvariantCulture);

    private static string Decimal(DnsMasterFileToken token)
    {
        var text = Plain(token);
        return IsDecimal(text) ? text : throw new FormatException("A number is decimal digits.");
    }

    private static DnsMasterFileToken Single(IReadOnlyList<DnsMasterFileToken> tokens) => Exactly(tokens, 1)[0];

    private static IReadOnlyList<DnsMasterFileToken> Exactly(IReadOnlyList<DnsMasterFileToken> tokens, int count) =>
        tokens.Count == count ? tokens : throw new FormatException($"The RDATA has {count} fields.");

    private static string Plain(DnsMasterFileToken token) =>
        token.IsQuoted ? throw Fault($"a quoted \"{token.Text}\" where a word belongs") : token.Text;

    private static bool IsDecimal(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);

    private static DnsZoneFileException Fault(string fault) => new(null, fault);
}
