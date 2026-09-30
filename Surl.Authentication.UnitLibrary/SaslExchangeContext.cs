using System.Security.Cryptography.X509Certificates;
using Surl.Kerberos;

namespace Surl.Authentication;

/// <summary>
/// What a <see cref="SaslMechanismExchange"/> needs from the policy that started it and from
/// the client's start.
/// </summary>
/// <param name="Policy">The policy: its accounts, <c>--allow-anonymous</c> and refusal delay.</param>
/// <param name="Scheme">The listen URL's scheme, which names the Kerberos service SASL <c>GSSAPI</c> answers (ADR-0057, decision 2).</param>
/// <param name="Mechanism">The mechanism's registered name, upper case: the login note's method.</param>
/// <param name="InitialResponse">The decoded initial response, or <see langword="null"/> when none was sent.</param>
/// <param name="ClientCertificate">
/// The connection's verified TLS client certificate, or <see langword="null"/> when it has none:
/// the identity SASL <c>EXTERNAL</c> logs in as (ADR-0049, section 4).
/// </param>
internal sealed record SaslExchangeContext(
    AuthenticationPolicy Policy,
    string Scheme,
    string Mechanism,
    ReadOnlyMemory<byte>? InitialResponse,
    X509Certificate2? ClientCertificate)
{
    /// <summary>
    /// The configured accounts.
    /// </summary>
    public AccountBook Accounts => Policy.Settings.Accounts;

    /// <summary>
    /// <c>--allow-anonymous</c>: every step runs, and the login ends accepted unchecked.
    /// </summary>
    public bool IsUnchecked => Policy.Settings.AllowAnonymous;

    /// <summary>
    /// Where a challenge's random bytes come from.
    /// </summary>
    public ISaslNonceSource NonceSource => Policy.NonceSource;

    /// <summary>
    /// A <c>CRAM-MD5</c> challenge (ADR-0049, section 5).
    /// </summary>
    /// <returns>The challenge, <c>&lt;</c>16 hex digits<c>.</c>Unix seconds<c>@surl&gt;</c>.</returns>
    public string CreateTimestamp() => Policy.CreateTimestamp();

    /// <summary>
    /// A new NTLM handshake over the accounts, with the policy's server challenges (ADR-0039).
    /// </summary>
    /// <returns>The handshake, with no server challenge issued yet.</returns>
    public NtlmHandshake StartNtlmHandshake() => new(Accounts, Policy.NtlmServerChallenges);

    /// <summary>
    /// What checks a client's Kerberos AP-REQ against the <c>--keytab</c> keys; the policy offers
    /// and starts SASL <c>GSSAPI</c> only when there is one (ADR-0057, decision 9).
    /// </summary>
    public KerberosAcceptor KerberosAcceptor => Policy.Settings.KerberosAcceptor!;

    /// <summary>
    /// Waits the refusal delay on the policy's clock (ADR-0032, section 8).
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The wait.</returns>
    public Task WaitRefusalDelayAsync(CancellationToken cancellationToken) =>
        Policy.WaitRefusalDelayAsync(cancellationToken);
}
