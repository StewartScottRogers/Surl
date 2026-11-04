namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>derefAliases</c> of a <c>SearchRequest</c> (RFC 4511, section 4.5.1.3).
/// </summary>
internal enum LdapDerefAliases
{
    /// <summary><c>neverDerefAliases</c> (0).</summary>
    NeverDerefAliases = 0,

    /// <summary><c>derefInSearching</c> (1).</summary>
    DerefInSearching = 1,

    /// <summary><c>derefFindingBaseObj</c> (2).</summary>
    DerefFindingBaseObj = 2,

    /// <summary><c>derefAlways</c> (3).</summary>
    DerefAlways = 3,
}
