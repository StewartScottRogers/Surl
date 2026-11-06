namespace Surl.Protocol.Ldap;

/// <summary>
/// Which <c>AttributeValueAssertion</c> choice an <see cref="LdapComparisonFilter"/> is, by its
/// context tag in RFC 4511's <c>Filter</c>.
/// </summary>
internal enum LdapComparison
{
    /// <summary><c>equalityMatch</c> [3].</summary>
    EqualityMatch = 3,

    /// <summary><c>greaterOrEqual</c> [5].</summary>
    GreaterOrEqual = 5,

    /// <summary><c>lessOrEqual</c> [6].</summary>
    LessOrEqual = 6,

    /// <summary><c>approxMatch</c> [8].</summary>
    ApproxMatch = 8,
}
