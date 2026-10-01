namespace Surl.Protocol.Ldap;

/// <summary>
/// A <c>BindRequest</c> (RFC 4511, section 4.2).
/// </summary>
/// <param name="Version">The protocol version, from 1 to 127; LDAPv3 clients send 3.</param>
/// <param name="Name">The DN to bind as; empty for an anonymous bind.</param>
/// <param name="Authentication">The <c>authentication</c> choice.</param>
internal sealed record LdapBindRequest(int Version, string Name, LdapBindAuthentication Authentication) : LdapProtocolOperation;
