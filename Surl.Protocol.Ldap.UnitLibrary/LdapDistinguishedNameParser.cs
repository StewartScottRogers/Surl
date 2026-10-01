using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Parses the string form of a DN (RFC 4514, section 3): RDNs separated by <c>,</c>, AVAs within
/// one separated by <c>+</c>, <c>\</c>-escapes and <c>\XX</c> hex pairs, and <c>#</c> BER values.
/// Spaces around the separators and <c>=</c> are dropped, as section 4 lets a parser do.
/// </summary>
internal sealed class LdapDistinguishedNameParser
{
    private const string EscapableCharacters = "\"+,;<>\\ #=";
    private const string CharactersThatMustBeEscaped = "\"<>;\\";

    private readonly string text;
    private int position;

    private LdapDistinguishedNameParser(string text)
    {
        this.text = text;
    }

    private bool IsAtEnd => position == text.Length;

    private char Current => text[position];

    private bool IsAtValueEnd => IsAtEnd || Current is ',' or '+';

    /// <summary>
    /// Parses a DN into its RDNs.
    /// </summary>
    /// <param name="text">The DN as written.</param>
    /// <returns>The RDNs, leftmost first; <see langword="null"/> when the text is not a DN.</returns>
    public static IReadOnlyList<IReadOnlyList<LdapAttributeValueAssertion>>? TryParse(string text)
    {
        var parser = new LdapDistinguishedNameParser(text);
        parser.SkipSpaces();
        return parser.IsAtEnd ? [] : parser.TryReadRelativeDistinguishedNames();
    }

    private static bool IsHexDigit(char character) => char.IsAsciiHexDigit(character);

    private static bool IsNumericOid(string type) =>
        type.Split('.') is { Length: >= 2 } numbers
        && numbers.All(number => number.Length > 0 && number.All(char.IsAsciiDigit) && (number.Length == 1 || number[0] != '0'));

    private List<IReadOnlyList<LdapAttributeValueAssertion>>? TryReadRelativeDistinguishedNames()
    {
        var relativeDistinguishedNames = new List<IReadOnlyList<LdapAttributeValueAssertion>>();
        while (TryReadRelativeDistinguishedName() is { } relativeDistinguishedName)
        {
            relativeDistinguishedNames.Add(relativeDistinguishedName);
            if (IsAtEnd)
            {
                return relativeDistinguishedNames;
            }

            position++;
        }

        return null;
    }

    private List<LdapAttributeValueAssertion>? TryReadRelativeDistinguishedName()
    {
        var assertions = new List<LdapAttributeValueAssertion>();
        while (TryReadAttributeValueAssertion() is { } assertion)
        {
            assertions.Add(assertion);
            if (IsAtEnd || Current == ',')
            {
                return assertions;
            }

            position++;
        }

        return null;
    }

    private LdapAttributeValueAssertion? TryReadAttributeValueAssertion()
    {
        SkipSpaces();
        var type = TryReadType();
        SkipSpaces();
        if (type is null || IsAtEnd || Current != '=')
        {
            return null;
        }

        position++;
        SkipSpaces();
        return !IsAtEnd && Current == '#' ? TryReadBerValue(type) : TryReadStringValue(type);
    }

    private static bool IsTypeCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '-' or '.';

    private static bool IsDescriptor(string type) =>
        type.Length > 0 && char.IsAsciiLetter(type[0]) && !type.Contains('.', StringComparison.Ordinal);

    private static bool IsEvenHex(string hex) => hex.Length > 0 && hex.Length % 2 == 0;

    private string? TryReadType()
    {
        var type = ReadWhile(IsTypeCharacter);
        return IsDescriptor(type) || IsNumericOid(type) ? type : null;
    }

    private LdapAttributeValueAssertion? TryReadBerValue(string type)
    {
        position++;
        var hex = ReadWhile(IsHexDigit);
        SkipSpaces();
        return IsEvenHex(hex) && IsAtValueEnd ? new LdapAttributeValueAssertion(type, Convert.FromHexString(hex), IsBerEncoded: true) : null;
    }

    private string ReadWhile(Func<char, bool> isPart)
    {
        var start = position;
        while (!IsAtEnd && isPart(Current))
        {
            position++;
        }

        return text[start..position];
    }

    private LdapAttributeValueAssertion? TryReadStringValue(string type)
    {
        var value = new List<byte>();
        var significantLength = 0;
        while (!IsAtValueEnd)
        {
            if (!TryReadValueCharacter(value, ref significantLength))
            {
                return null;
            }
        }

        var bytes = value.GetRange(0, significantLength).ToArray();
        return LdapMatchingRules.TryDecodeUtf8(bytes) is null ? null : new LdapAttributeValueAssertion(type, bytes, IsBerEncoded: false);
    }

    private bool TryReadValueCharacter(List<byte> value, ref int significantLength)
    {
        if (Current == '\\')
        {
            var isEscape = TryReadEscape(value);
            significantLength = value.Count;
            return isEscape;
        }

        if (CharactersThatMustBeEscaped.Contains(Current, StringComparison.Ordinal))
        {
            return false;
        }

        var length = char.IsHighSurrogate(Current) && position + 1 < text.Length ? 2 : 1;
        value.AddRange(Encoding.UTF8.GetBytes(text.Substring(position, length)));
        significantLength = Current == ' ' ? significantLength : value.Count;
        position += length;
        return true;
    }

    private bool TryReadEscape(List<byte> value)
    {
        var next = position + 1;
        if (next + 1 < text.Length && IsHexDigit(text[next]) && IsHexDigit(text[next + 1]))
        {
            value.Add(Convert.ToByte(text.Substring(next, 2), 16));
            position += 3;
            return true;
        }

        if (next < text.Length && EscapableCharacters.Contains(text[next], StringComparison.Ordinal))
        {
            value.Add((byte)text[next]);
            position += 2;
            return true;
        }

        return false;
    }

    private void SkipSpaces()
    {
        while (!IsAtEnd && Current == ' ')
        {
            position++;
        }
    }
}
