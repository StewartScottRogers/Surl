using System.Buffers;
using System.Formats.Asn1;
using System.Globalization;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// Reads an RFC 4515 string filter and writes it as the BER <c>Filter</c> of RFC 4511 section 4.5.1.7
/// (ADR-0149, decision 11). Every filter form is read: <c>&amp;</c>, <c>|</c>, <c>!</c>, equality,
/// substrings, presence, <c>&gt;=</c>, <c>&lt;=</c>, <c>~=</c> and extensible match, with <c>\HH</c> escapes.
/// </summary>
internal static class LdapFilterEncoder
{
    private const int MaxDepth = 16;

    private static readonly SearchValues<char> AttributeCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-.;");

    /// <summary>
    /// Encodes <paramref name="filter"/>.
    /// </summary>
    /// <param name="filter">The RFC 4515 string representation, with its outer parentheses.</param>
    /// <returns>The BER <c>Filter</c>, or <see langword="null"/> when <paramref name="filter"/> is malformed.</returns>
    public static byte[]? TryEncode(string filter)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        var position = 0;

        return TryWriteFilter(writer, filter, ref position, 0) && position == filter.Length ? writer.Encode() : null;
    }

    /// <summary>
    /// Whether <paramref name="text"/> is an attribute description or numeric OID with options:
    /// letters, digits, <c>-</c>, <c>.</c> and <c>;</c>, starting with a letter or digit.
    /// </summary>
    /// <param name="text">The attribute.</param>
    /// <returns>Whether it is well formed.</returns>
    internal static bool IsAttributeDescription(ReadOnlySpan<char> text) =>
        text.Length > 0 && char.IsAsciiLetterOrDigit(text[0]) && !text.ContainsAnyExcept(AttributeCharacters);

    private static Asn1Tag Context(int number, bool isConstructed) =>
        new(TagClass.ContextSpecific, number, isConstructed);

    private static bool TryWriteFilter(AsnWriter writer, string filter, ref int position, int depth)
    {
        if (depth >= MaxDepth || position >= filter.Length - 1 || filter[position] != '(')
        {
            return false;
        }

        position++;
        var isWritten = filter[position] switch
        {
            '&' => TryWriteList(writer, filter, ref position, depth, 0),
            '|' => TryWriteList(writer, filter, ref position, depth, 1),
            '!' => TryWriteNot(writer, filter, ref position, depth),
            _ => TryWriteItem(writer, filter, ref position),
        };

        if (!isWritten || position >= filter.Length || filter[position] != ')')
        {
            return false;
        }

        position++;
        return true;
    }

    private static bool TryWriteList(AsnWriter writer, string filter, ref int position, int depth, int tagNumber)
    {
        position++;
        using var set = writer.PushSetOf(Context(tagNumber, true));
        var count = 0;
        while (position < filter.Length && filter[position] == '(')
        {
            if (!TryWriteFilter(writer, filter, ref position, depth + 1))
            {
                return false;
            }

            count++;
        }

        return count > 0;
    }

    private static bool TryWriteNot(AsnWriter writer, string filter, ref int position, int depth)
    {
        position++;
        using var not = writer.PushSequence(Context(2, true));

        return TryWriteFilter(writer, filter, ref position, depth + 1);
    }

    private static bool TryWriteItem(AsnWriter writer, string filter, ref int position)
    {
        var end = filter.IndexOf(')', position);
        if (end < 0)
        {
            return false;
        }

        var item = filter.AsSpan(position, end - position);
        position = end;
        var equals = item.IndexOf('=');
        if (equals <= 0)
        {
            return false;
        }

        var left = item[..equals];
        var right = item[(equals + 1)..];

        return left[^1] switch
        {
            ':' => TryWriteExtensible(writer, left[..^1].ToString(), right),
            '~' => TryWriteAssertion(writer, 8, left[..^1], right),
            '>' => TryWriteAssertion(writer, 5, left[..^1], right),
            '<' => TryWriteAssertion(writer, 6, left[..^1], right),
            _ => TryWriteEqualityOrSubstrings(writer, left, right),
        };
    }

    private static bool TryWriteAssertion(AsnWriter writer, int tagNumber, ReadOnlySpan<char> attribute, ReadOnlySpan<char> value)
    {
        if (!IsAttributeDescription(attribute) || !TryDecodeValue(value, out var bytes))
        {
            return false;
        }

        using var assertion = writer.PushSequence(Context(tagNumber, true));
        writer.WriteOctetString(Encoding.UTF8.GetBytes(attribute.ToString()));
        writer.WriteOctetString(bytes);

        return true;
    }

    private static bool TryWriteEqualityOrSubstrings(AsnWriter writer, ReadOnlySpan<char> attribute, ReadOnlySpan<char> value)
    {
        if (!IsAttributeDescription(attribute))
        {
            return false;
        }

        if (value.SequenceEqual("*"))
        {
            writer.WriteOctetString(Encoding.UTF8.GetBytes(attribute.ToString()), Context(7, false));

            return true;
        }

        return value.Contains('*')
            ? TryWriteSubstrings(writer, attribute.ToString(), value.ToString())
            : TryWriteAssertion(writer, 3, attribute, value);
    }

    private static bool TryWriteSubstrings(AsnWriter writer, string attribute, string value)
    {
        var decoded = new List<byte[]>();
        foreach (var part in value.Split('*'))
        {
            if (!TryDecodeValue(part, out var bytes))
            {
                return false;
            }

            decoded.Add(bytes);
        }

        // Adjacent stars leave an empty middle part, which RFC 4515 does not allow.
        if (decoded.Skip(1).SkipLast(1).Any(part => part.Length == 0))
        {
            return false;
        }

        using var substrings = writer.PushSequence(Context(4, true));
        writer.WriteOctetString(Encoding.UTF8.GetBytes(attribute));
        using var sequence = writer.PushSequence();
        for (var index = 0; index < decoded.Count; index++)
        {
            if (decoded[index].Length > 0)
            {
                var tagNumber = index == 0 ? 0 : index == decoded.Count - 1 ? 2 : 1;
                writer.WriteOctetString(decoded[index], Context(tagNumber, false));
            }
        }

        return true;
    }

    private static bool TryWriteExtensible(AsnWriter writer, string left, ReadOnlySpan<char> value)
    {
        if (!TryReadExtensibleName(left, out var attribute, out var rule, out var isDn) || !TryDecodeValue(value, out var bytes))
        {
            return false;
        }

        using var match = writer.PushSequence(Context(9, true));
        if (rule is not null)
        {
            writer.WriteOctetString(Encoding.UTF8.GetBytes(rule), Context(1, false));
        }

        if (attribute.Length > 0)
        {
            writer.WriteOctetString(Encoding.UTF8.GetBytes(attribute), Context(2, false));
        }

        writer.WriteOctetString(bytes, Context(3, false));
        if (isDn)
        {
            writer.WriteBoolean(true, Context(4, false));
        }

        return true;
    }

    // Reads "attribute[:dn][:rule]": the attribute may be empty when a rule is given.
    private static bool TryReadExtensibleName(string left, out string attribute, out string? rule, out bool isDn)
    {
        var segments = left.Split(':');
        attribute = segments[0];
        isDn = false;
        rule = null;
        foreach (var segment in segments.Skip(1))
        {
            if (!isDn && string.Equals(segment, "dn", StringComparison.OrdinalIgnoreCase))
            {
                isDn = true;
            }
            else if (rule is null && IsAttributeDescription(segment))
            {
                rule = segment;
            }
            else
            {
                return false;
            }
        }

        return attribute.Length > 0 ? IsAttributeDescription(attribute) : rule is not null;
    }

    // Decodes a value: \HH is the byte HH, every other character its UTF-8 bytes. A lone backslash fails.
    private static bool TryDecodeValue(ReadOnlySpan<char> text, out byte[] bytes)
    {
        var buffer = new List<byte>();
        var literal = new StringBuilder();
        bytes = [];
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\')
            {
                literal.Append(text[index]);
                continue;
            }

            if (index + 2 >= text.Length
                || !byte.TryParse(text.Slice(index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var escaped))
            {
                return false;
            }

            buffer.AddRange(Encoding.UTF8.GetBytes(literal.ToString()));
            literal.Clear();
            buffer.Add(escaped);
            index += 2;
        }

        buffer.AddRange(Encoding.UTF8.GetBytes(literal.ToString()));
        bytes = [.. buffer];
        return true;
    }
}
