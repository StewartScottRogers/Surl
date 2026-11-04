namespace Surl.Protocol.Ldap;

/// <summary>
/// The matching rules (RFC 4517) the directory compares values with (ADR-0072 decision 1).
/// </summary>
internal enum LdapMatchingRule
{
    /// <summary><c>caseIgnoreMatch</c> (2.5.13.2), with its ordering and substrings rules.</summary>
    CaseIgnore,

    /// <summary><c>caseExactMatch</c> (2.5.13.5), reached only by an <c>extensibleMatch</c> naming it.</summary>
    CaseExact,

    /// <summary><c>integerMatch</c> (2.5.13.14), with <c>integerOrderingMatch</c>.</summary>
    Integer,

    /// <summary><c>octetStringMatch</c> (2.5.13.17), with <c>octetStringOrderingMatch</c>.</summary>
    OctetString,
}
