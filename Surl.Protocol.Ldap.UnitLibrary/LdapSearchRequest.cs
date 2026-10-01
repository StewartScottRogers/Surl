namespace Surl.Protocol.Ldap;

/// <summary>
/// A <c>SearchRequest</c> (RFC 4511, section 4.5.1).
/// </summary>
/// <param name="BaseObject">The DN the search starts at.</param>
/// <param name="Scope">How far below the base the search reaches.</param>
/// <param name="DerefAliases">When aliases are dereferenced.</param>
/// <param name="SizeLimit">The most entries to return; 0 means no client limit.</param>
/// <param name="TimeLimit">The most seconds to spend; 0 means no client limit.</param>
/// <param name="TypesOnly">Whether only attribute descriptions, and no values, are returned.</param>
/// <param name="Filter">The filter entries must match.</param>
/// <param name="Attributes">The attribute selection, in the order sent; empty means all user attributes.</param>
internal sealed record LdapSearchRequest(
    string BaseObject,
    LdapSearchScope Scope,
    LdapDerefAliases DerefAliases,
    int SizeLimit,
    int TimeLimit,
    bool TypesOnly,
    LdapFilter Filter,
    IReadOnlyList<string> Attributes) : LdapProtocolOperation;
