namespace Surl.Kerberos;

/// <summary>
/// What <see cref="KerberosAcceptor" /> made of a client's AP-REQ: a security context when it
/// was accepted, or the reason it was refused (ADR-0057 decision 4), for the verbose log.
/// </summary>
/// <param name="Context">The accepted context, or <see langword="null" /> when refused.</param>
/// <param name="RefusalReason">
/// Why the AP-REQ was refused, such as <c>ticket expired</c> or <c>clock skew</c>, or
/// <see langword="null" /> when accepted. It never holds a key byte or a decrypted field.
/// </param>
public sealed record KerberosAcceptResult(KerberosSecurityContext? Context, string? RefusalReason);
