namespace Surl.Protocol.Ldap;

/// <summary>
/// One <c>attributeTypeAndValue</c> of a DN's relative distinguished name (RFC 4514, section 2.3).
/// </summary>
/// <param name="Type">The attribute type, as written.</param>
/// <param name="Value">The value, unescaped: its UTF-8 bytes, or the BER bytes of a <c>#</c> value.</param>
/// <param name="IsBerEncoded">Whether the value was written as <c>#</c> and hex, the BER encoding of the value.</param>
internal sealed record LdapAttributeValueAssertion(string Type, byte[] Value, bool IsBerEncoded);
