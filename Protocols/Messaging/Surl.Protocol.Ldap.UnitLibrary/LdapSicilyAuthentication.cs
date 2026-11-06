namespace Surl.Protocol.Ldap;

/// <summary>
/// One of the Sicily authentication choices <c>WinLDAP</c> sends for <c>--ntlm</c>
/// (MS-ADTS section 5.1.1.1.3; ADR-0072 decision 4): <c>[9]</c> <c>sicilyPackageDiscovery</c>,
/// <c>[10]</c> <c>sicilyNegotiate</c> holding an NTLM <c>NEGOTIATE_MESSAGE</c>, or <c>[11]</c>
/// <c>sicilyResponse</c> holding its <c>AUTHENTICATE_MESSAGE</c>.
/// </summary>
/// <param name="Choice">Which choice was sent.</param>
/// <param name="Token">The choice's octets: empty for package discovery, the NTLM message otherwise.</param>
internal sealed record LdapSicilyAuthentication(LdapSicilyChoice Choice, byte[] Token) : LdapBindAuthentication;
