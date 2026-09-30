namespace Surl.Authentication;

/// <summary>
/// HTTP Negotiate, RFC 4559 over <c>Authorization: Negotiate</c> (ADR-0032 sections 3, 4 and 11,
/// ADR-0040), carrying NTLM: the bare <c>Negotiate</c> challenge, then on one connection the NTLM
/// handshake of ADR-0039, bare or inside SPNEGO (RFC 4178). Kerberos inside Negotiate is later
/// work; until it lands a Kerberos token is refused. The handshake lives in the verifier
/// <see cref="StartConnection"/> returns, so it dies with the connection.
/// </summary>
public sealed class NegotiateAuthenticationMethod : IHttpAuthenticationMethod
{
    private static readonly IReadOnlyList<string> Challenges = ["Negotiate"];

    private readonly AccountBook accounts;
    private readonly INtlmServerChallengeSource serverChallenges;

    /// <summary>
    /// Checks Negotiate answers against <paramref name="accounts"/>, with random NTLM server challenges.
    /// </summary>
    /// <param name="accounts">The configured accounts.</param>
    public NegotiateAuthenticationMethod(AccountBook accounts)
        : this(accounts, RandomNtlmServerChallengeSource.Instance)
    {
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
    public IHttpCredentialVerifier StartConnection() => new NegotiateConnectionVerifier(accounts, serverChallenges);
}
