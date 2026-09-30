namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>extensibleMatch</c> choice, a <c>MatchingRuleAssertion</c> (RFC 4511, section 4.5.1.7.7).
/// At least one of <paramref name="MatchingRule"/> and <paramref name="Type"/> is present.
/// </summary>
/// <param name="MatchingRule">The <c>matchingRule</c>; <see langword="null"/> when absent.</param>
/// <param name="Type">The attribute <c>type</c>; <see langword="null"/> when absent.</param>
/// <param name="MatchValue">The <c>matchValue</c>.</param>
/// <param name="DnAttributes">The <c>dnAttributes</c> flag; <see langword="false"/> when absent, its default.</param>
internal sealed record LdapExtensibleMatchFilter(string? MatchingRule, string? Type, byte[] MatchValue, bool DnAttributes) : LdapFilter;
