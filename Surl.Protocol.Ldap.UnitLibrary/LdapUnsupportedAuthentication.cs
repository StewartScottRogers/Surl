using System.Formats.Asn1;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Any authentication choice but <c>simple</c> and <c>sasl</c>, such as the reserved tags 1 and 2,
/// which the server answers with <see cref="LdapResultCode.AuthMethodNotSupported"/>.
/// </summary>
/// <param name="Tag">The choice's tag as sent.</param>
internal sealed record LdapUnsupportedAuthentication(Asn1Tag Tag) : LdapBindAuthentication;
