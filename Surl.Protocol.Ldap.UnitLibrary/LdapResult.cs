namespace Surl.Protocol.Ldap;

/// <summary>
/// An <c>LDAPResult</c> (RFC 4511, section 4.1.9), the part every response but a search entry
/// or reference carries.
/// </summary>
/// <param name="ResultCode">The <c>resultCode</c>.</param>
/// <param name="MatchedDn">The <c>matchedDN</c>; empty when there is none.</param>
/// <param name="DiagnosticMessage">The <c>diagnosticMessage</c>; empty when there is none.</param>
/// <param name="Referral">The <c>referral</c> URIs; <see langword="null"/> or empty leaves the field out.</param>
internal sealed record LdapResult(
    LdapResultCode ResultCode,
    string MatchedDn,
    string DiagnosticMessage,
    IReadOnlyList<string>? Referral = null);
