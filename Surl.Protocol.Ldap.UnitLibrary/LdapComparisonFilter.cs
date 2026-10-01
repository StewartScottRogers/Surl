namespace Surl.Protocol.Ldap;

/// <summary>
/// The four choices that carry an <c>AttributeValueAssertion</c>: <c>equalityMatch</c>,
/// <c>greaterOrEqual</c>, <c>lessOrEqual</c> and <c>approxMatch</c>.
/// </summary>
/// <param name="Comparison">Which of the four choices it is.</param>
/// <param name="AttributeDescription">The attribute compared.</param>
/// <param name="AssertionValue">The value compared with.</param>
internal sealed record LdapComparisonFilter(LdapComparison Comparison, string AttributeDescription, byte[] AssertionValue) : LdapFilter;
