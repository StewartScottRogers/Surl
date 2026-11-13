namespace Surl.Authentication;

/// <summary>
/// The scope of an LDAP search (RFC 4511 section 4.5.1.2); the values are the wire values.
/// </summary>
public enum LdapSearchScope
{
    /// <summary>Only the base object.</summary>
    Base = 0,

    /// <summary>The base object's immediate children.</summary>
    OneLevel = 1,

    /// <summary>The base object and everything below it.</summary>
    Subtree = 2,
}
