namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>present</c> choice: the entry holds the attribute.
/// </summary>
/// <param name="AttributeDescription">The attribute that must be present.</param>
internal sealed record LdapPresentFilter(string AttributeDescription) : LdapFilter;
