namespace Surl.Protocol.Ldap;

/// <summary>
/// <c>simple</c> authentication (RFC 4511, section 4.2): a password, empty for an anonymous or
/// unauthenticated bind.
/// </summary>
/// <param name="Password">The password's bytes.</param>
internal sealed record LdapSimpleAuthentication(byte[] Password) : LdapBindAuthentication;
