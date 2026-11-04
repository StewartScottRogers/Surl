using System.Formats.Asn1;
using static Surl.Protocol.Ldap.LdapBerFieldReader;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Decodes a <c>SearchRequest</c>'s <c>Filter</c> (RFC 4511, section 4.5.1.7), every choice,
/// refusing one nested deeper than a bound before descending into it, since the nesting is the
/// peer's to choose and each level is a recursive call.
/// </summary>
/// <param name="maxDepth">
/// How deep a filter may nest: a filter with no <c>and</c>, <c>or</c> or <c>not</c> is depth 1, and
/// each of those adds one to the filters inside it.
/// </param>
internal sealed class LdapFilterDecoder(int maxDepth)
{
    private const int AndTag = 0;
    private const int OrTag = 1;
    private const int NotTag = 2;
    private const int SubstringsTag = 4;
    private const int PresentTag = 7;
    private const int ExtensibleMatchTag = 9;

    /// <summary>
    /// Reads the filter at the top of a search.
    /// </summary>
    /// <param name="reader">The reader over the <c>SearchRequest</c>, positioned at its filter.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="LdapDecodeException">The filter is malformed or nests too deep.</exception>
    public LdapFilter ReadFilter(AsnReader reader) => ReadFilter(reader, 1);

    private static LdapComparisonFilter ReadComparisonFilter(AsnReader reader, Asn1Tag tag)
    {
        var comparison = tag.TagValue switch
        {
            (int)LdapComparison.EqualityMatch => LdapComparison.EqualityMatch,
            (int)LdapComparison.GreaterOrEqual => LdapComparison.GreaterOrEqual,
            (int)LdapComparison.LessOrEqual => LdapComparison.LessOrEqual,
            (int)LdapComparison.ApproxMatch => LdapComparison.ApproxMatch,
            _ => throw new LdapDecodeException(LdapDecodeOutcome.UnexpectedTag),
        };
        var assertion = ReadSequence(reader, LdapTags.Context(tag.TagValue, isConstructed: true));
        var attribute = ReadString(assertion, Asn1Tag.PrimitiveOctetString);
        var value = ReadOctetString(assertion, Asn1Tag.PrimitiveOctetString);
        EnsureEnd(assertion);

        return new LdapComparisonFilter(comparison, attribute, value);
    }

    private static LdapSubstringsFilter ReadSubstringsFilter(AsnReader reader)
    {
        var filter = ReadSequence(reader, LdapTags.Context(SubstringsTag, isConstructed: true));
        var attribute = ReadString(filter, Asn1Tag.PrimitiveOctetString);
        var substrings = ReadSequence(filter, Asn1Tag.Sequence);
        EnsureEnd(filter);
        if (!substrings.HasData)
        {
            throw new LdapDecodeException(LdapDecodeOutcome.InvalidValue);
        }

        var initial = ReadOptionalSubstring(substrings, 0);
        var any = new List<byte[]>();
        while (IsNext(substrings, LdapTags.Context(1)))
        {
            any.Add(ReadOctetString(substrings, LdapTags.Context(1)));
        }

        var final = ReadOptionalSubstring(substrings, 2);

        // Whatever is left is an initial after an any, anything after the final, or another tag.
        return substrings.HasData
            ? throw new LdapDecodeException(LdapDecodeOutcome.UnexpectedTag)
            : new LdapSubstringsFilter(attribute, initial, any, final);
    }

    private static byte[]? ReadOptionalSubstring(AsnReader substrings, int number) =>
        IsNext(substrings, LdapTags.Context(number)) ? ReadOctetString(substrings, LdapTags.Context(number)) : null;

    private static LdapExtensibleMatchFilter ReadExtensibleMatchFilter(AsnReader reader)
    {
        var assertion = ReadSequence(reader, LdapTags.Context(ExtensibleMatchTag, isConstructed: true));
        var matchingRule = IsNext(assertion, LdapTags.Context(1)) ? ReadString(assertion, LdapTags.Context(1)) : null;
        var type = IsNext(assertion, LdapTags.Context(2)) ? ReadString(assertion, LdapTags.Context(2)) : null;
        var matchValue = ReadOctetString(assertion, LdapTags.Context(3));
        var dnAttributes = assertion.HasData && ReadBoolean(assertion, LdapTags.Context(4));
        EnsureEnd(assertion);

        // RFC 4511 section 4.5.1.7.7: without a matchingRule, the type must be present.
        return matchingRule is null && type is null
            ? throw new LdapDecodeException(LdapDecodeOutcome.InvalidValue)
            : new LdapExtensibleMatchFilter(matchingRule, type, matchValue, dnAttributes);
    }

    private LdapFilter ReadFilter(AsnReader reader, int depth)
    {
        if (depth > maxDepth)
        {
            throw new LdapDecodeException(LdapDecodeOutcome.FilterTooDeep);
        }

        var tag = PeekTag(reader);
        if (tag.TagClass != TagClass.ContextSpecific)
        {
            throw new LdapDecodeException(LdapDecodeOutcome.UnexpectedTag);
        }

        return tag.TagValue <= NotTag ? ReadCompositeFilter(reader, tag.TagValue, depth) : ReadItemFilter(reader, tag);
    }

    // The choices that hold other filters: and, or and not.
    private LdapFilter ReadCompositeFilter(AsnReader reader, int number, int depth) => number switch
    {
        AndTag => new LdapAndFilter(ReadFilterSet(reader, AndTag, depth)),
        OrTag => new LdapOrFilter(ReadFilterSet(reader, OrTag, depth)),
        _ => ReadNotFilter(reader, depth),
    };

    // The choices that test one attribute.
    private static LdapFilter ReadItemFilter(AsnReader reader, Asn1Tag tag) => tag.TagValue switch
    {
        SubstringsTag => ReadSubstringsFilter(reader),
        PresentTag => new LdapPresentFilter(ReadString(reader, LdapTags.Context(PresentTag))),
        ExtensibleMatchTag => ReadExtensibleMatchFilter(reader),
        _ => ReadComparisonFilter(reader, tag),
    };

    private List<LdapFilter> ReadFilterSet(AsnReader reader, int number, int depth)
    {
        var set = ReadSequence(reader, LdapTags.Context(number, isConstructed: true));
        var filters = new List<LdapFilter>();
        while (set.HasData)
        {
            filters.Add(ReadFilter(set, depth + 1));
        }

        return filters;
    }

    private LdapNotFilter ReadNotFilter(AsnReader reader, int depth)
    {
        var not = ReadSequence(reader, LdapTags.Context(NotTag, isConstructed: true));
        var filter = ReadFilter(not, depth + 1);
        EnsureEnd(not);

        return new LdapNotFilter(filter);
    }
}
