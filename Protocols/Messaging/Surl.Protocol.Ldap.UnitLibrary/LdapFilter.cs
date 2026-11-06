namespace Surl.Protocol.Ldap;

/// <summary>
/// One <c>Filter</c> of a <c>SearchRequest</c> (RFC 4511, section 4.5.1.7), one subtype per choice.
/// </summary>
internal abstract record LdapFilter;
