namespace Surl.Protocol.Ldap;

/// <summary>
/// One entry of the directory (ADR-0072 decision 1): a DN and an ordered list of attributes, with
/// no schema checking.
/// </summary>
/// <param name="Dn">The entry's DN; a search returns it as written.</param>
/// <param name="Attributes">The entry's attributes, in order.</param>
internal sealed record LdapEntry(LdapDistinguishedName Dn, IReadOnlyList<LdapAttribute> Attributes);
