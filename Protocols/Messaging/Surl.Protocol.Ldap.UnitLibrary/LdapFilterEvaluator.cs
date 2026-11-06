namespace Surl.Protocol.Ldap;

/// <summary>
/// Evaluates a search filter against one entry, three-valued (RFC 4511, section 4.5.1.7;
/// ADR-0072 decision 1): an item naming a type the entry lacks is FALSE, a rule that does not apply
/// is Undefined, and <c>and</c>, <c>or</c> and <c>not</c> combine as the RFC says.
/// </summary>
internal static class LdapFilterEvaluator
{
    /// <summary>
    /// Evaluates a filter against an entry.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="dn">The entry's DN, which <c>extensibleMatch</c> with <c>dnAttributes</c> reads.</param>
    /// <param name="attributes">The entry's attributes.</param>
    /// <returns>The filter's value; only <see cref="LdapFilterResult.True"/> returns the entry.</returns>
    public static LdapFilterResult Evaluate(LdapFilter filter, LdapDistinguishedName dn, IReadOnlyList<LdapAttribute> attributes) => filter switch
    {
        LdapAndFilter and => EvaluateAnd(and.Filters, dn, attributes),
        LdapOrFilter or => EvaluateOr(or.Filters, dn, attributes),
        LdapNotFilter not => Negate(Evaluate(not.Filter, dn, attributes)),
        LdapPresentFilter present => IsPresent(LdapAttributeDescription.Parse(present.AttributeDescription), attributes),
        LdapComparisonFilter comparison => EvaluateComparison(comparison, attributes),
        LdapSubstringsFilter substrings => EvaluateValues(
            LdapAttributeDescription.Parse(substrings.AttributeDescription),
            attributes,
            (rule, value) => LdapMatchingRules.MatchSubstrings(rule, value, substrings)),
        LdapExtensibleMatchFilter extensible => EvaluateExtensible(extensible, dn, attributes),
        _ => LdapFilterResult.Undefined,
    };

    /// <summary>
    /// Combines two filter values as <c>or</c> does: TRUE when either is, else Undefined when
    /// either is, else FALSE.
    /// </summary>
    /// <param name="left">One value.</param>
    /// <param name="right">The other.</param>
    /// <returns>The combined value.</returns>
    public static LdapFilterResult Or(LdapFilterResult left, LdapFilterResult right) =>
        left == LdapFilterResult.True || right == LdapFilterResult.True ? LdapFilterResult.True
        : left == LdapFilterResult.Undefined || right == LdapFilterResult.Undefined ? LdapFilterResult.Undefined
        : LdapFilterResult.False;

    /// <summary>
    /// Evaluates a match over every value of the attributes a description names: TRUE when any
    /// value matches, else Undefined when any is Undefined, else FALSE (FALSE too when the entry
    /// has no such attribute).
    /// </summary>
    /// <param name="description">The description the filter item names; <see langword="null"/> names every attribute.</param>
    /// <param name="attributes">The entry's attributes.</param>
    /// <param name="match">The match for one value, given the attribute's own rule.</param>
    /// <returns>The item's value.</returns>
    public static LdapFilterResult EvaluateValues(
        LdapAttributeDescription? description,
        IReadOnlyList<LdapAttribute> attributes,
        Func<LdapMatchingRule, byte[], LdapFilterResult> match)
    {
        var result = LdapFilterResult.False;
        foreach (var attribute in attributes.Where(attribute => description is null || description.Names(attribute.Description)))
        {
            var rule = LdapMatchingRules.ForDescription(attribute.Description);
            foreach (var value in attribute.Values)
            {
                result = Or(result, match(rule, value));
            }
        }

        return result;
    }

    private static LdapFilterResult EvaluateAnd(IReadOnlyList<LdapFilter> filters, LdapDistinguishedName dn, IReadOnlyList<LdapAttribute> attributes) =>
        Negate(EvaluateOr(filters.Select(filter => new LdapNotFilter(filter)).ToArray(), dn, attributes));

    private static LdapFilterResult EvaluateOr(IReadOnlyList<LdapFilter> filters, LdapDistinguishedName dn, IReadOnlyList<LdapAttribute> attributes) =>
        filters.Aggregate(LdapFilterResult.False, (result, filter) => Or(result, Evaluate(filter, dn, attributes)));

    private static LdapFilterResult Negate(LdapFilterResult result) => result switch
    {
        LdapFilterResult.True => LdapFilterResult.False,
        LdapFilterResult.False => LdapFilterResult.True,
        _ => LdapFilterResult.Undefined,
    };

    private static LdapFilterResult IsPresent(LdapAttributeDescription description, IReadOnlyList<LdapAttribute> attributes) =>
        attributes.Any(attribute => description.Names(attribute.Description)) ? LdapFilterResult.True : LdapFilterResult.False;

    private static LdapFilterResult EvaluateComparison(LdapComparisonFilter comparison, IReadOnlyList<LdapAttribute> attributes)
    {
        Func<int, bool> holds = comparison.Comparison switch
        {
            LdapComparison.GreaterOrEqual => order => order >= 0,
            LdapComparison.LessOrEqual => order => order <= 0,
            _ => order => order == 0,
        };

        return EvaluateValues(
            LdapAttributeDescription.Parse(comparison.AttributeDescription),
            attributes,
            (rule, value) => ToResult(LdapMatchingRules.Compare(rule, value, comparison.AssertionValue), holds));
    }

    private static LdapFilterResult EvaluateExtensible(LdapExtensibleMatchFilter extensible, LdapDistinguishedName dn, IReadOnlyList<LdapAttribute> attributes)
    {
        LdapMatchingRule? namedRule = null;
        if (extensible.MatchingRule is not null)
        {
            if (!LdapMatchingRules.TryFindByName(extensible.MatchingRule, out var rule))
            {
                return LdapFilterResult.Undefined;
            }

            namedRule = rule;
        }

        var description = extensible.Type is null ? null : LdapAttributeDescription.Parse(extensible.Type);
        Func<LdapMatchingRule, byte[], LdapFilterResult> match = (ownRule, value) =>
            ToResult(LdapMatchingRules.Compare(namedRule ?? ownRule, value, extensible.MatchValue), order => order == 0);
        var result = EvaluateValues(description, attributes, match);
        return extensible.DnAttributes ? Or(result, EvaluateValues(description, AttributesOf(dn), match)) : result;
    }

    private static IReadOnlyList<LdapAttribute> AttributesOf(LdapDistinguishedName dn) =>
        dn.RelativeDistinguishedNames
            .SelectMany(relativeDistinguishedName => relativeDistinguishedName)
            .Select(assertion => new LdapAttribute(assertion.Type, [assertion.Value]))
            .ToArray();

    private static LdapFilterResult ToResult(int? order, Func<int, bool> holds) =>
        order is int value ? (holds(value) ? LdapFilterResult.True : LdapFilterResult.False) : LdapFilterResult.Undefined;
}
