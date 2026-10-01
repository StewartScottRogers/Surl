namespace Surl.Protocol.Ldap;

/// <summary>
/// An <c>ExtendedRequest</c> (RFC 4511, section 4.12), such as StartTLS.
/// </summary>
/// <param name="RequestName">The request's OID, as its dotted-decimal text.</param>
/// <param name="RequestValue">The <c>requestValue</c>; <see langword="null"/> when absent.</param>
internal sealed record LdapExtendedRequest(string RequestName, byte[]? RequestValue) : LdapProtocolOperation;
