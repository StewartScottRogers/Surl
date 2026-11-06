namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>and</c> choice: every filter in the set must match. An empty set is the absolute true
/// filter of RFC 4526.
/// </summary>
/// <param name="Filters">The filters, in the order sent.</param>
internal sealed record LdapAndFilter(IReadOnlyList<LdapFilter> Filters) : LdapFilter;
