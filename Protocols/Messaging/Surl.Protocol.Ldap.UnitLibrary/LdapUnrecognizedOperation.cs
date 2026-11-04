using System.Formats.Asn1;

namespace Surl.Protocol.Ldap;

/// <summary>
/// A <c>protocolOp</c> Surl does not decode - a modify, add, delete, compare or any other -
/// reported by its tag so the server can still answer it.
/// </summary>
/// <param name="Tag">The operation's tag as sent.</param>
internal sealed record LdapUnrecognizedOperation(Asn1Tag Tag) : LdapProtocolOperation;
