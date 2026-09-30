namespace Surl.Kerberos;

/// <summary>
/// The two keys an accepted context holds (ADR-0057 decision 4, step 7): the ticket's session
/// key, which encrypts the AP-REP, and the context key, which protects the RFC 4121 tokens: the
/// authenticator's subkey when it has one, else the session key.
/// </summary>
/// <param name="SessionProfile">The session key's encryption type.</param>
/// <param name="SessionKey">The ticket's session key.</param>
/// <param name="ContextProfile">The context key's encryption type.</param>
/// <param name="ContextKey">The context key.</param>
internal sealed record KerberosSessionKeys(
    KerberosEncryptionProfile SessionProfile,
    byte[] SessionKey,
    KerberosEncryptionProfile ContextProfile,
    byte[] ContextKey);
