namespace Surl.Protocol.Ldap;

/// <summary>
/// One decoded <c>LDAPMessage</c> a client sent (RFC 4511, section 4.1.1).
/// </summary>
/// <param name="MessageId">The <c>messageID</c>, from 0 to 2147483647.</param>
/// <param name="Operation">The request the message carries.</param>
/// <param name="Controls">The <c>controls</c>, in the order sent; empty when the message had none.</param>
internal sealed record LdapMessage(int MessageId, LdapProtocolOperation Operation, IReadOnlyList<LdapControl> Controls);
