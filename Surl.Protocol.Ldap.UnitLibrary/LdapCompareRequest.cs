namespace Surl.Protocol.Ldap;

/// <summary>
/// A <c>CompareRequest</c> (RFC 4511, section 4.10): does the entry hold the asserted value?
/// </summary>
/// <param name="Entry">The DN of the entry compared, as sent.</param>
/// <param name="AttributeDescription">The attribute description asserted, as sent.</param>
/// <param name="AssertionValue">The value asserted.</param>
internal sealed record LdapCompareRequest(string Entry, string AttributeDescription, byte[] AssertionValue) : LdapProtocolOperation;
