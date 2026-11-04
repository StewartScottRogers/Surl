using System.Globalization;
using System.Numerics;
using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Which matching rule an attribute takes, and how each rule compares values (RFC 4517, with
/// RFC 4518's insignificant-space handling; ADR-0072 decision 1).
/// </summary>
internal static class LdapMatchingRules
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly HashSet<string> OctetStringTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "userPassword", "jpegPhoto", "userCertificate", "cACertificate", "photo", "audio",
    };

    private static readonly HashSet<string> IntegerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "uidNumber", "gidNumber", "shadowLastChange", "shadowMin", "shadowMax", "shadowWarning", "shadowInactive", "shadowExpire",
    };

    private static readonly Dictionary<string, LdapMatchingRule> RulesByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["caseIgnoreMatch"] = LdapMatchingRule.CaseIgnore,
        ["2.5.13.2"] = LdapMatchingRule.CaseIgnore,
        ["caseExactMatch"] = LdapMatchingRule.CaseExact,
        ["2.5.13.5"] = LdapMatchingRule.CaseExact,
        ["octetStringMatch"] = LdapMatchingRule.OctetString,
        ["2.5.13.17"] = LdapMatchingRule.OctetString,
        ["integerMatch"] = LdapMatchingRule.Integer,
        ["2.5.13.14"] = LdapMatchingRule.Integer,
    };

    /// <summary>
    /// The rule an attribute's values are matched by: <c>octetStringMatch</c> for the binary types
    /// and any description with the <c>binary</c> option, <c>integerMatch</c> for RFC 2307's
    /// numbers, <c>caseIgnoreMatch</c> for every other type.
    /// </summary>
    /// <param name="description">The attribute's description.</param>
    /// <returns>The rule.</returns>
    public static LdapMatchingRule ForDescription(LdapAttributeDescription description)
    {
        if (OctetStringTypes.Contains(description.Type) || description.HasOption("binary"))
        {
            return LdapMatchingRule.OctetString;
        }

        return IntegerTypes.Contains(description.Type) ? LdapMatchingRule.Integer : LdapMatchingRule.CaseIgnore;
    }

    /// <summary>
    /// Finds the rule an <c>extensibleMatch</c> names, by its name or its numeric OID.
    /// </summary>
    /// <param name="nameOrOid">The <c>matchingRule</c> as sent.</param>
    /// <param name="rule">The rule, when it is one the directory applies.</param>
    /// <returns><see langword="true"/> when the directory applies the rule.</returns>
    public static bool TryFindByName(string nameOrOid, out LdapMatchingRule rule) => RulesByName.TryGetValue(nameOrOid, out rule);

    /// <summary>
    /// Compares a value with an assertion value by the rule's ordering.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="value">The entry's value.</param>
    /// <param name="assertion">The assertion value.</param>
    /// <returns>Negative, zero or positive as the value sorts before, equal to or after the assertion; <see langword="null"/> when either cannot be read by the rule.</returns>
    public static int? Compare(LdapMatchingRule rule, byte[] value, byte[] assertion) => rule switch
    {
        LdapMatchingRule.OctetString => Math.Sign(value.AsSpan().SequenceCompareTo(assertion)),
        LdapMatchingRule.Integer => CompareIntegers(value, assertion),
        _ => CompareStrings(rule, value, assertion),
    };

    /// <summary>
    /// Matches a value against a <c>substrings</c> filter by the rule's substrings rule; only the
    /// string rules have one.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="value">The entry's value.</param>
    /// <param name="filter">The filter.</param>
    /// <returns>The filter's value for this one value.</returns>
    public static LdapFilterResult MatchSubstrings(LdapMatchingRule rule, byte[] value, LdapSubstringsFilter filter)
    {
        if (rule is LdapMatchingRule.OctetString or LdapMatchingRule.Integer)
        {
            return LdapFilterResult.Undefined;
        }

        var prepared = TryPrepare(rule, value, trim: true);
        var parts = TryPrepareParts(rule, filter);
        if (prepared is null || parts is null)
        {
            return LdapFilterResult.Undefined;
        }

        return ContainsInOrder(prepared, parts[0], parts[1..^1], parts[^1]) ? LdapFilterResult.True : LdapFilterResult.False;
    }

    /// <summary>
    /// The value's normal form under the rule, as DN comparison uses it: the prepared string for
    /// the string rules, the canonical integer for <c>integerMatch</c>, and hex for the octets of
    /// <c>octetStringMatch</c> and of any value the rule cannot read.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="value">The value.</param>
    /// <returns>The normal form.</returns>
    public static string NormalForm(LdapMatchingRule rule, byte[] value) => rule switch
    {
        LdapMatchingRule.OctetString => Convert.ToHexString(value),
        LdapMatchingRule.Integer => TryParseInteger(value) is BigInteger number ? number.ToString(CultureInfo.InvariantCulture) : Convert.ToHexString(value),
        _ => TryPrepare(rule, value, trim: true) ?? Convert.ToHexString(value),
    };

    /// <summary>
    /// Decodes strict UTF-8.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text; <see langword="null"/> when the bytes are not UTF-8.</returns>
    public static string? TryDecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static int? CompareIntegers(byte[] value, byte[] assertion) =>
        TryParseInteger(value) is BigInteger left && TryParseInteger(assertion) is BigInteger right
            ? left.CompareTo(right)
            : null;

    private static int? CompareStrings(LdapMatchingRule rule, byte[] value, byte[] assertion) =>
        TryPrepare(rule, value, trim: true) is string left && TryPrepare(rule, assertion, trim: true) is string right
            ? Math.Sign(string.CompareOrdinal(left, right))
            : null;

    private static BigInteger? TryParseInteger(byte[] value) =>
        BigInteger.TryParse(Encoding.UTF8.GetString(value), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    private static string? TryPrepare(LdapMatchingRule rule, byte[] value, bool trim)
    {
        var text = TryDecodeUtf8(value);
        return text is null ? null : Prepare(text, rule == LdapMatchingRule.CaseIgnore, trim);
    }

    private static string[]? TryPrepareParts(LdapMatchingRule rule, LdapSubstringsFilter filter)
    {
        byte[][] parts = [filter.Initial ?? [], .. filter.Any, filter.Final ?? []];
        var prepared = parts.Select(part => TryPrepare(rule, part, trim: false)).ToArray();
        return prepared.Contains(null) ? null : prepared.OfType<string>().ToArray();
    }

    private static string Prepare(string text, bool foldCase, bool trim)
    {
        var prepared = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (!IsRepeatedSpace(prepared, character))
            {
                prepared.Append(foldCase ? char.ToLowerInvariant(char.ToUpperInvariant(character)) : character);
            }
        }

        return trim ? prepared.ToString().Trim(' ') : prepared.ToString();
    }

    private static bool IsRepeatedSpace(StringBuilder prepared, char character) =>
        character == ' ' && prepared.Length > 0 && prepared[^1] == ' ';

    private static bool ContainsInOrder(string value, string initial, IReadOnlyList<string> any, string final)
    {
        if (!value.StartsWith(initial, StringComparison.Ordinal) || !value.EndsWith(final, StringComparison.Ordinal))
        {
            return false;
        }

        var position = initial.Length;
        var end = value.Length - final.Length;
        foreach (var part in any)
        {
            var found = position <= end ? value.IndexOf(part, position, end - position, StringComparison.Ordinal) : -1;
            if (found < 0)
            {
                return false;
            }

            position = found + part.Length;
        }

        return position <= end;
    }
}
