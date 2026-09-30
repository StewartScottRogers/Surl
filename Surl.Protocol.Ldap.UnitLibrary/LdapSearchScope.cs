namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>scope</c> of a <c>SearchRequest</c> (RFC 4511, section 4.5.1.2).
/// </summary>
internal enum LdapSearchScope
{
    /// <summary><c>baseObject</c> (0): the base entry alone.</summary>
    BaseObject = 0,

    /// <summary><c>singleLevel</c> (1): the base entry's immediate subordinates.</summary>
    SingleLevel = 1,

    /// <summary><c>wholeSubtree</c> (2): the base entry and everything below it.</summary>
    WholeSubtree = 2,
}
