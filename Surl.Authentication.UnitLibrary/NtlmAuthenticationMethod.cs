namespace Surl.Authentication;

/// <summary>
/// HTTP NTLM, [MS-NLMP] over <c>Authorization: NTLM</c> (ADR-0032 sections 3 and 4, ADR-0039):
/// the bare <c>NTLM</c> challenge, then on one connection a <c>CHALLENGE_MESSAGE</c> for the
/// client's <c>NEGOTIATE_MESSAGE</c> and the check of its NTLMv2 <c>AUTHENTICATE_MESSAGE</c>
/// against the account's NT hash. The handshake lives in the verifier
/// <see cref="StartConnection"/> returns, so it dies with the connection.
/// </summary>
public sealed class NtlmAuthenticationMethod : IHttpAuthenticationMethod
{
    private static readonly IReadOnlyList<string> Challenges = ["NTLM"];

    private readonly AccountBook accounts;
    private readonly INtlmServerChallengeSource serverChallenges;

    /// <summary>
    /// Checks NTLM answers against <paramref name="accounts"/>, with random server challenges.
    /// </summary>
    /// <param name="accounts">The configured accounts.</param>
    public NtlmAuthenticationMethod(AccountBook accounts)
        : this(accounts, RandomNtlmServerChallengeSource.Instance)
    {
    }

    internal NtlmAuthenticationMethod(AccountBook accounts, INtlmServerChallengeSource serverChallenges)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        this.accounts = accounts;
        this.serverChallenges = serverChallenges;
    }

    /// <inheritdoc/>
    public AuthenticationMethod Method => AuthenticationMethod.Ntlm;

    /// <summary>
    /// The one challenge of ADR-0032 section 4: <c>NTLM</c>, with no token.
    /// </summary>
    /// <returns><c>NTLM</c>.</returns>
    public IReadOnlyList<string> CreateChallenges() => Challenges;

    /// <summary>
    /// Starts one connection's handshake, with no server challenge issued yet.
    /// </summary>
    /// <returns>The connection's verifier.</returns>
    public IHttpCredentialVerifier StartConnection() => new NtlmConnectionVerifier(accounts, serverChallenges);
}
