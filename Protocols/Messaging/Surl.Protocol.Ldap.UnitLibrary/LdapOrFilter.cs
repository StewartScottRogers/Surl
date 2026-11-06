namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>or</c> choice: at least one filter in the set must match. An empty set is the absolute
/// false filter of RFC 4526.
/// </summary>
/// <param name="Filters">The filters, in the order sent.</param>
internal sealed record LdapOrFilter(IReadOnlyList<LdapFilter> Filters) : LdapFilter;
