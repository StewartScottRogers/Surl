namespace Surl.Protocol.Ldap;

/// <summary>
/// What a search over the directory answers: the entries to send, then the
/// <c>searchResultDone</c>'s result.
/// </summary>
/// <param name="Entries">The entries, in directory order.</param>
/// <param name="Result">The <c>LDAPResult</c> of the <c>searchResultDone</c>.</param>
internal sealed record LdapSearchOutcome(IReadOnlyList<LdapSearchResultEntry> Entries, LdapResult Result);
