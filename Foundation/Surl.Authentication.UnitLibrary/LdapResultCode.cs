namespace Surl.Authentication;

/// <summary>
/// An LDAP <c>resultCode</c> (RFC 4511 section 4.1.9), an ASN.1 ENUMERATED; any value a directory sends
/// is carried, named or not. Only <see cref="Success"/> is named: the others are compared as numbers.
/// </summary>
internal enum LdapResultCode
{
    /// <summary>The operation succeeded.</summary>
    Success = 0,
}
