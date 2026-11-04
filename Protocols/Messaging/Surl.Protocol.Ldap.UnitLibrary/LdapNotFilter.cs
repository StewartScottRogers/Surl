namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>not</c> choice: the inner filter must not match.
/// </summary>
/// <param name="Filter">The filter negated.</param>
internal sealed record LdapNotFilter(LdapFilter Filter) : LdapFilter;
