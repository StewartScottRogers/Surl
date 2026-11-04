namespace Surl.Protocol.Ldap;

/// <summary>
/// An <c>AbandonRequest</c> (RFC 4511, section 4.11).
/// </summary>
/// <param name="MessageIdToAbandon">The <c>messageID</c> of the operation to abandon.</param>
internal sealed record LdapAbandonRequest(int MessageIdToAbandon) : LdapProtocolOperation;
