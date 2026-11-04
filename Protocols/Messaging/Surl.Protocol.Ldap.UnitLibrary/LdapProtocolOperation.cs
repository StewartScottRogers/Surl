namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>protocolOp</c> of a decoded <c>LDAPMessage</c>: one of the requests Surl reads
/// (RFC 4511, section 4.2 onwards), or <see cref="LdapUnrecognizedOperation"/> for any other.
/// </summary>
internal abstract record LdapProtocolOperation;
