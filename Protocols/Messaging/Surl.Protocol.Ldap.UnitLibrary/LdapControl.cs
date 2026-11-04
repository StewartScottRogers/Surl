namespace Surl.Protocol.Ldap;

/// <summary>
/// One <c>Control</c> attached to a request (RFC 4511, section 4.1.11).
/// </summary>
/// <param name="ControlType">The control's OID, as its dotted-decimal text.</param>
/// <param name="Criticality">The <c>criticality</c>; <see langword="false"/> when absent, its default.</param>
/// <param name="ControlValue">The <c>controlValue</c>; <see langword="null"/> when absent.</param>
internal sealed record LdapControl(string ControlType, bool Criticality, byte[]? ControlValue);
