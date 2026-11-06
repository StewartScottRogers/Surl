using System.Buffers;
using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Reads the directory's file (ADR-0072 decision 1): RFC 2849 LDIF, content records only, UTF-8
/// without a byte order mark, into records in file order.
/// </summary>
/// <remarks>
/// An optional first line <c>version: 1</c>; lines end in LF or CRLF; a line starting with one
/// space continues the line before it, the space dropped; a line starting <c>#</c> is a comment,
/// its continuation lines too; records are separated by empty lines. A record is a <c>dn</c> line
/// then <c>&lt;description&gt;: &lt;value&gt;</c> or <c>&lt;description&gt;:: &lt;base64&gt;</c>
/// lines; one description's values, wherever they stand in the record, are one attribute, in
/// file order. Change records and URL values are refused.
/// </remarks>
internal static class LdifReader
{
    private const string VersionPrefix = "version:";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly SearchValues<char> Base64Characters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=");

    private static readonly SearchValues<char> KeyCharacters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-");

    /// <summary>RFC 2849's SAFE-CHAR: ASCII but NUL, CR and LF.</summary>
    private static readonly SearchValues<char> SafeCharacters =
        SearchValues.Create(Enumerable.Range(1, 0x7F).Where(code => code is not 10 and not 13).Select(code => (char)code).ToArray());

    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Reads an LDIF file's bytes.
    /// </summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <returns>The records, in file order.</returns>
    /// <exception cref="LdifFormatException">The file is not LDIF content the directory can hold.</exception>
    public static IReadOnlyList<LdifRecord> Read(ReadOnlySpan<byte> bytes)
    {
        var blocks = BlocksOf(UnfoldedLinesOf(PhysicalLinesOf(bytes)));
        SkipVersion(blocks);
        return blocks.Select(RecordOf).ToArray();
    }

    private static List<LdifLine> PhysicalLinesOf(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Utf8ByteOrderMark))
        {
            throw new LdifFormatException(1, LdifFaultText.NotUtf8);
        }

        var lines = new List<LdifLine>();
        for (var number = 1; ; number++)
        {
            var end = bytes.IndexOf((byte)'\n');
            if (end < 0)
            {
                lines.Add(Decode(number, bytes));
                return lines;
            }

            lines.Add(Decode(number, bytes[..end]));
            bytes = bytes[(end + 1)..];
        }
    }

    private static LdifLine Decode(int number, ReadOnlySpan<byte> bytes)
    {
        var content = bytes.EndsWith((byte)'\r') ? bytes[..^1] : bytes;
        try
        {
            return new LdifLine(number, StrictUtf8.GetString(content));
        }
        catch (DecoderFallbackException)
        {
            throw new LdifFormatException(number, LdifFaultText.NotUtf8);
        }
    }

    /// <summary>
    /// Unfolds continuation lines into the line they continue and drops comments; an empty line,
    /// a record separator, is kept as it is.
    /// </summary>
    private static List<LdifLine> UnfoldedLinesOf(List<LdifLine> physicalLines)
    {
        var unfolded = new List<LdifLine>();
        StringBuilder? open = null;
        var openNumber = 0;
        foreach (var line in physicalLines)
        {
            if (line.Text.StartsWith(' '))
            {
                (open ?? throw new LdifFormatException(line.Number, LdifFaultText.ContinuationWithNothing)).Append(line.Text, 1, line.Text.Length - 1);
                continue;
            }

            AddUnlessComment(unfolded, openNumber, open);
            open = line.Text.Length == 0 ? null : new StringBuilder(line.Text);
            openNumber = line.Number;
            if (open is null)
            {
                unfolded.Add(line);
            }
        }

        AddUnlessComment(unfolded, openNumber, open);
        return unfolded;
    }

    private static void AddUnlessComment(List<LdifLine> unfolded, int number, StringBuilder? line)
    {
        if (line is not null && line[0] != '#')
        {
            unfolded.Add(new LdifLine(number, line.ToString()));
        }
    }

    private static List<List<LdifLine>> BlocksOf(List<LdifLine> unfoldedLines)
    {
        var blocks = new List<List<LdifLine>> { new() };
        foreach (var line in unfoldedLines)
        {
            if (line.Text.Length == 0)
            {
                blocks.Add([]);
                continue;
            }

            blocks[^1].Add(line);
        }

        return blocks.Where(block => block.Count > 0).ToList();
    }

    private static void SkipVersion(List<List<LdifLine>> blocks)
    {
        if (blocks.Count == 0 || !blocks[0][0].Text.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var version = blocks[0][0];
        if (!version.Text.AsSpan(VersionPrefix.Length).TrimStart(' ').SequenceEqual("1"))
        {
            throw new LdifFormatException(version.Number, LdifFaultText.VersionNotOne);
        }

        blocks[0].RemoveAt(0);
        if (blocks[0].Count == 0)
        {
            blocks.RemoveAt(0);
        }
    }

    private static LdifRecord RecordOf(List<LdifLine> block)
    {
        var dnLine = block[0];
        var (description, value) = ValueLineOf(dnLine);
        if (!description.Equals("dn", StringComparison.OrdinalIgnoreCase))
        {
            throw new LdifFormatException(dnLine.Number, LdifFaultText.RecordWithoutDn);
        }

        return new LdifRecord(dnLine.Number, new LdapEntry(DnOf(dnLine.Number, value), AttributesOf(block.Skip(1))));
    }

    private static LdapDistinguishedName DnOf(int number, byte[] value)
    {
        var text = TryDecode(value);
        return text is not null && LdapDistinguishedName.TryParse(text, out var dn)
            ? dn
            : throw new LdifFormatException(number, LdifFaultText.DnNotRfc4514);
    }

    private static string? TryDecode(byte[] value)
    {
        try
        {
            return StrictUtf8.GetString(value);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static LdapAttribute[] AttributesOf(IEnumerable<LdifLine> lines)
    {
        var valuesByDescription = new Dictionary<string, List<byte[]>>(StringComparer.OrdinalIgnoreCase);
        var descriptions = new List<string>();
        foreach (var line in lines)
        {
            var (description, value) = ValueLineOf(line);
            if (description.Equals("changetype", StringComparison.OrdinalIgnoreCase))
            {
                throw new LdifFormatException(line.Number, LdifFaultText.ChangeRecord);
            }

            if (!valuesByDescription.TryGetValue(description, out var values))
            {
                values = [];
                valuesByDescription.Add(description, values);
                descriptions.Add(description);
            }

            values.Add(value);
        }

        return descriptions.Select(description => new LdapAttribute(description, valuesByDescription[description])).ToArray();
    }

    private static (string Description, byte[] Value) ValueLineOf(LdifLine line)
    {
        var colon = line.Text.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0 || !IsDescription(line.Text[..colon]))
        {
            throw new LdifFormatException(line.Number, LdifFaultText.NotDescriptionValue);
        }

        var rest = line.Text.AsSpan(colon + 1);
        var value = rest switch
        {
            [':', ..] => Base64ValueOf(line.Number, rest[1..].TrimStart(' ')),
            ['<', ..] => throw new LdifFormatException(line.Number, LdifFaultText.UrlValue),
            _ => SafeValueOf(line.Number, rest.TrimStart(' ')),
        };
        return (line.Text[..colon], value);
    }

    private static byte[] Base64ValueOf(int number, ReadOnlySpan<char> text)
    {
        var buffer = new byte[text.Length];
        return !text.ContainsAnyExcept(Base64Characters) && Convert.TryFromBase64Chars(text, buffer, out var length)
            ? buffer[..length]
            : throw new LdifFormatException(number, LdifFaultText.BadBase64);
    }

    private static byte[] SafeValueOf(int number, ReadOnlySpan<char> text) =>
        IsSafeString(text)
            ? Encoding.ASCII.GetBytes(text.ToString())
            : throw new LdifFormatException(number, LdifFaultText.NotSafeString);

    /// <summary>
    /// RFC 2849's SAFE-STRING: ASCII but NUL, CR and LF, not starting with a colon or
    /// <c>&lt;</c>. The leading spaces are already gone, and no LF is left in a line.
    /// </summary>
    private static bool IsSafeString(ReadOnlySpan<char> text) =>
        (text.IsEmpty || text[0] is not (':' or '<')) && !text.ContainsAnyExcept(SafeCharacters);

    /// <summary>
    /// RFC 4512's attribute description: a name (<c>[A-Za-z][A-Za-z0-9-]*</c>) or a numeric OID,
    /// then <c>;</c>-separated options of letters, digits and hyphens.
    /// </summary>
    private static bool IsDescription(string text)
    {
        var parts = text.Split(';');
        return (IsName(parts[0]) || IsNumericOid(parts[0])) && parts[1..].All(IsOption);
    }

    private static bool IsName(string text) => text.Length > 0 && char.IsAsciiLetter(text[0]) && IsOption(text);

    private static bool IsOption(string text) => text.Length > 0 && !text.AsSpan().ContainsAnyExcept(KeyCharacters);

    private static bool IsNumericOid(string text)
    {
        var numbers = text.Split('.');
        return numbers.Length > 1 && numbers.All(IsNumber);
    }

    private static bool IsNumber(string text) =>
        text.Length > 0 && !text.AsSpan().ContainsAnyExceptInRange('0', '9') && (text.Length == 1 || text[0] != '0');
}
