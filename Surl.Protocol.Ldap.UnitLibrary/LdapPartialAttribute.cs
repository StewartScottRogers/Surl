namespace Surl.Protocol.Ldap;

/// <summary>
/// A <c>PartialAttribute</c> of a <c>SearchResultEntry</c> (RFC 4511, section 4.1.7).
/// </summary>
/// <param name="Type">The attribute description.</param>
/// <param name="Values">The values, in the order they are sent; empty when only the type is returned.</param>
internal sealed record LdapPartialAttribute(string Type, IReadOnlyList<byte[]> Values);
