namespace Surl.Protocol.Ldap;

/// <summary>
/// <c>sasl</c> authentication (RFC 4511, section 4.2): a mechanism and its optional credentials.
/// </summary>
/// <param name="Mechanism">The SASL mechanism's name.</param>
/// <param name="Credentials">The mechanism's credentials; <see langword="null"/> when absent.</param>
internal sealed record LdapSaslAuthentication(string Mechanism, byte[]? Credentials) : LdapBindAuthentication;
