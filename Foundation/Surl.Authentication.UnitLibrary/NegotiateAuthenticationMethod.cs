using Surl.Kerberos;

namespace Surl.Authentication;

/// <summary>
/// HTTP Negotiate, RFC 4559 over <c>Authorization: Negotiate</c> (ADR-0032 sections 3, 4 and 11,
/// ADR-0040), carrying NTLM: the bare <c>Negotiate</c> challenge, then on one connection the NTLM
/// handshake of ADR-0039, bare or inside SPNEGO (RFC 4178). Given a Kerberos acceptor
/// (<c>--keytab</c>), it also carries Kerberos, bare or inside SPNEGO, in one leg (ADR-0057
/// decision 8); without one a Kerberos token is refused. The handshake lives in the verifier
/// <see cref="StartConnection"/> returns, so it dies with the connection.
/// </summary>
public sealed class NegotiateAuthenticationMethod : IHttpAuthenticationMethod
{
    private static readonly IReadOnlyList<string> Challenges = ["Negotiate"];

    private readonly AccountBook accounts;
    private readonly INtlmServerChallengeSource serverChallenges;
    private readonly NegotiateKerberosLogin? kerberosLogin;
    private readonly bool isUnchecked;

    /// <summary>
    /// Checks Negotiate answers against <paramref name="accounts"/>, carrying NTLM only, with
    /// random NTLM server challenges.
    /// </summary>
    /// <param name="accounts">The configured accounts.</param>
    public NegotiateAuthenticationMethod(AccountBook accounts)
        : this(accounts, RandomNtlmServerChallengeSource.Instance)
    {
    }

    /// <summary>
    /// Checks Negotiate answers against <paramref name="accounts"/>, carrying Kerberos too when
    /// <paramref name="kerberosAcceptor"/> is given, with random NTLM server challenges. With both
    /// an acceptor and <paramref name="allowAnonymous"/>, a Kerberos ticket must still decrypt, only
    /// its account match is skipped, and every other token is served unchecked.
    /// </summary>
    /// <param name="accounts">The configured accounts.</param>
    /// <param name="kerberosAcceptor">The <c>--keytab</c> acceptor, or <see langword="null"/> without one.</param>
    /// <param name="allowAnonymous"><c>--allow-anonymous</c>.</param>
    public NegotiateAuthenticationMethod(AccountBook accounts, KerberosAcceptor? kerberosAcceptor, bool allowAnonymous)
        : this(accounts, RandomNtlmServerChallengeSource.Instance)
    {
        isUnchecked = allowAnonymous && kerberosAcceptor is not null;
        kerberosLogin = kerberosAcceptor is null ? null : new NegotiateKerberosLogin(kerberosAcceptor, accounts, allowAnonymous);
    }

    internal NegotiateAuthenticationMethod(AccountBook accounts, INtlmServerChallengeSource serverChallenges)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        this.accounts = accounts;
        this.serverChallenges = serverChallenges;
    }

    /// <inheritdoc/>
    public AuthenticationMethod Method => AuthenticationMethod.Negotiate;

    /// <summary>
    /// The one challenge of ADR-0032 section 4: <c>Negotiate</c>, with no token.
    /// </summary>
    /// <returns><c>Negotiate</c>.</returns>
    public IReadOnlyList<string> CreateChallenges() => Challenges;

    /// <summary>
    /// Starts one connection's handshake, with no server challenge issued yet.
    /// </summary>
    /// <returns>The connection's verifier.</returns>
    public IHttpCredentialVerifier StartConnection() =>
        new NegotiateConnectionVerifier(accounts, serverChallenges, kerberosLogin, isUnchecked);
}
