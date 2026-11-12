namespace Surl.Authentication;

/// <summary>
/// A search's <c>derefAliases</c> (RFC 4511 section 4.5.1.3); the wire value of the only choice made.
/// </summary>
internal enum LdapAliasDereferencing
{
    /// <summary>Aliases are never followed.</summary>
    Never = 0,
}
