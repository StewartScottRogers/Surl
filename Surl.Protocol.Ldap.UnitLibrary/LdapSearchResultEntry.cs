namespace Surl.Protocol.Ldap;

/// <summary>
/// One entry a search returns, ready for <see cref="LdapMessageEncoder.EncodeSearchResultEntry"/>.
/// </summary>
/// <param name="ObjectName">The entry's DN as written.</param>
/// <param name="Attributes">The selected attributes, with no values when the search asked for types only.</param>
internal sealed record LdapSearchResultEntry(string ObjectName, IReadOnlyList<LdapPartialAttribute> Attributes);
