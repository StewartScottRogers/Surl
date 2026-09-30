namespace Surl.Kerberos;

/// <summary>
/// Thrown inside <see cref="KerberosAcceptor" /> when a check of ADR-0057 decision 4 fails;
/// caught there and returned as <see cref="KerberosAcceptResult.RefusalReason" />.
/// </summary>
/// <param name="reason">The refusal reason, never holding a key byte or a decrypted field.</param>
internal sealed class KerberosRefusalException(string reason) : Exception(reason)
{
    /// <summary>Gets the refusal reason.</summary>
    public string Reason { get; } = reason;
}
