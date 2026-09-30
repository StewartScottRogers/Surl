using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// HTTP Digest, RFC 7616 (ADR-0032 sections 3 and 4, ADR-0036): three challenges, MD5 then
/// SHA-256 then SHA-512-256, sharing one fresh nonce, and the check of a <c>qop=auth</c>
/// answer under any of them or their <c>-sess</c> forms. An answer is accepted only when its
/// <c>uri</c> is the request target, its nonce was issued here and is fresh, its
/// <c>response</c> matches an account (compared in fixed time) and its <c>nc</c> is higher than
/// any used before with that nonce. A right answer on an expired nonce gets the challenges
/// again with <c>stale=true</c>, undelayed. Digest holds no per-connection state: nonces are
/// the method's, so every connection shares this one verifier.
/// </summary>
public sealed class DigestAuthenticationMethod : IHttpAuthenticationMethod, IHttpCredentialVerifier
{
    /// <summary>
    /// The realm every Digest challenge names, and so the one every user hash covers
    /// (ADR-0032, section 4).
    /// </summary>
    public const string Realm = "surl";

    private static readonly HttpCredentialCheck Refused = new(HttpCredentialOutcome.Refused, null, []);

    private readonly AccountBook accounts;
    private readonly IDigestNonceBook nonces;

    /// <summary>
    /// Checks Digest answers against <paramref name="accounts"/>, with nonces that expire on
    /// <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="accounts">The configured accounts.</param>
    /// <param name="timeProvider">The clock nonces are issued and expired on.</param>
    public DigestAuthenticationMethod(AccountBook accounts, TimeProvider timeProvider)
        : this(accounts, new DigestNonceBook(timeProvider ?? throw new ArgumentNullException(nameof(timeProvider))))
    {
    }

    internal DigestAuthenticationMethod(AccountBook accounts, IDigestNonceBook nonces)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        this.accounts = accounts;
        this.nonces = nonces;
    }

    /// <inheritdoc/>
    public AuthenticationMethod Method => AuthenticationMethod.Digest;

    /// <summary>
    /// The three challenges of ADR-0032 section 4, each
    /// <c>Digest realm="surl", qop="auth", algorithm=&lt;A&gt;, nonce="&lt;nonce&gt;"</c>, with
    /// one new nonce.
    /// </summary>
    /// <returns>The MD5, SHA-256 and SHA-512-256 challenges, in that order.</returns>
    public IReadOnlyList<string> CreateChallenges() => CreateChallenges(nonces.Issue(), string.Empty);

    /// <inheritdoc/>
    public IHttpCredentialVerifier StartConnection() => this;

    /// <summary>
    /// Checks a Digest answer. A malformed answer, one Surl cannot check, a <c>uri</c> other
    /// than the request target, an unknown nonce, a wrong response or a replayed <c>nc</c> is
    /// refused, never thrown.
    /// </summary>
    /// <param name="credentials">The parameters after <c>Digest</c>.</param>
    /// <param name="request">The request the field arrived on: its method and target.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Accepted as the account, the stale challenges, or refused.</returns>
    public ValueTask<HttpCredentialCheck> VerifyAsync(
        string credentials, HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var answer = DigestAnswer.TryRead(credentials, request.Method);

        return ValueTask.FromResult(
            answer is null ? Refused : Verify(answer, request.Target) with { UserAsSent = answer.UserName });
    }

    private static IReadOnlyList<string> CreateChallenges(string nonce, string suffix) =>
        [.. Enum.GetValues<DigestAlgorithm>().Select(algorithm =>
            $"Digest realm=\"{Realm}\", qop=\"auth\", algorithm={DigestAlgorithmName.NameOf(algorithm)}, nonce=\"{nonce}\"{suffix}")];

    private HttpCredentialCheck Verify(DigestAnswer answer, string requestTarget)
    {
        if (answer.Inputs.Uri != requestTarget)
        {
            return Refused;
        }

        var nonceState = nonces.Check(answer.Inputs.Nonce);
        var accountName = nonceState == DigestNonceState.Unknown ? null : FindAnsweringAccount(answer);
        if (accountName is null)
        {
            return Refused;
        }

        if (nonceState == DigestNonceState.Expired)
        {
            return new HttpCredentialCheck(
                HttpCredentialOutcome.Continue, null, CreateChallenges(nonces.Issue(), ", stale=true"));
        }

        return nonces.TryRecordUse(answer.Inputs.Nonce, answer.NonceCount)
            ? new HttpCredentialCheck(HttpCredentialOutcome.Accepted, accountName, [])
            : Refused;
    }

    private string? FindAnsweringAccount(DigestAnswer answer)
    {
        var account = accounts.FindDigestAccount(answer.UserName);
        var response = Encoding.Latin1.GetBytes(answer.Response.ToLowerInvariant());

        // Both sets are always checked, so the work never depends on which one matched.
        var matches = account.UserHashSets
            .Select(userHashes => MatchesResponse(answer, userHashes, response))
            .Aggregate(false, (anyMatch, match) => anyMatch | match);

        return matches ? account.AccountName : null;
    }

    private bool MatchesResponse(DigestAnswer answer, IReadOnlyList<string> userHashes, byte[] response)
    {
        var algorithm = answer.Algorithm.Algorithm;
        var a1Hash = DigestCalculation.ComputeA1Hash(
            answer.Algorithm, userHashes[(int)algorithm], answer.Inputs.Nonce, answer.Inputs.Cnonce);
        var expected = DigestCalculation.ComputeResponse(algorithm, a1Hash, answer.Inputs);

        return accounts.SecretComparer.FixedTimeEquals(Encoding.Latin1.GetBytes(expected), response);
    }
}
