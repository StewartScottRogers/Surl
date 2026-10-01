namespace Surl.Protocol.Ldap;

/// <summary>
/// An <c>UnbindRequest</c> (RFC 4511, section 4.3): the client is closing the connection.
/// </summary>
internal sealed record LdapUnbindRequest : LdapProtocolOperation;
