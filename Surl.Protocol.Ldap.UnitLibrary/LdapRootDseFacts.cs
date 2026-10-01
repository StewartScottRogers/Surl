namespace Surl.Protocol.Ldap;

/// <summary>
/// What the root DSE says that depends on the connection (ADR-0072 decision 1): computed per
/// connection by the server and never stored.
/// </summary>
/// <param name="SaslMechanisms">The SASL mechanisms offered on this connection, in order; empty leaves <c>supportedSASLMechanisms</c> out.</param>
/// <param name="IsStartTlsOffered">Whether <c>StartTLS</c> is offered on this connection, which lists it in <c>supportedExtension</c>.</param>
internal sealed record LdapRootDseFacts(IReadOnlyList<string> SaslMechanisms, bool IsStartTlsOffered);
