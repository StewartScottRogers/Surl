namespace Surl.Protocol.Ldap;

/// <summary>
/// The Sicily authentication choices of a <c>BindRequest</c> (MS-ADTS section 5.1.1.1.3), each
/// its context tag number.
/// </summary>
internal enum LdapSicilyChoice
{
    /// <summary><c>[9]</c>: which packages the server offers; answered with their names as the matched DN.</summary>
    PackageDiscovery = 9,

    /// <summary><c>[10]</c>: an NTLM <c>NEGOTIATE_MESSAGE</c>.</summary>
    Negotiate = 10,

    /// <summary><c>[11]</c>: an NTLM <c>AUTHENTICATE_MESSAGE</c>.</summary>
    Response = 11,
}
