using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// HTTP Bearer, RFC 6750 (ADR-0032, sections 3 and 4): the challenge <c>Bearer realm="surl"</c>,
/// and the check of the token upstream curl's <c>--oauth2-bearer</c> sends against the
/// empty-name account's password (ADR-0032, section 1). The token is compared as the bytes
/// sent. Bearer holds no per-connection state, so every connection shares this one verifier.
/// </summary>
public sealed class BearerAuthenticationMethod : IHttpAuthenticationMethod, IHttpCredentialVerifier
{
    /// <summary>
    /// The one <c>WWW-Authenticate</c> value offering Bearer (ADR-0032, section 4).
    /// </summary>
    public const string Challenge = "Bearer realm=\"surl\"";

    private static readonly HttpCredentialCheck Accepted = new(HttpCredentialOutcome.Accepted, string.Empty, []);
    private static readonly HttpCredentialCheck Refused = new(HttpCredentialOutcome.Refused, null, []);

    private readonly AccountBook accounts;

    /// <summary>
    /// Checks Bearer tokens against <paramref name="accounts"/>.
    /// </summary>
    /// <param name="accounts">The configured accounts; the empty-name one holds the token.</param>
    public BearerAuthenticationMethod(AccountBook accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        this.accounts = accounts;
    }

    /// <inheritdoc/>
    public AuthenticationMethod Method => AuthenticationMethod.Bearer;

    /// <inheritdoc/>
    public IReadOnlyList<string> CreateChallenges() => [Challenge];

    /// <inheritdoc/>
    public IHttpCredentialVerifier StartConnection() => this;

    /// <summary>
    /// Checks <paramref name="credentials"/> as the token. The HTTP server reads each field
    /// value one byte per character (Latin-1), so the token is turned back into the bytes sent
    /// the same way. An empty token is refused, never matched.
    /// </summary>
    /// <param name="credentials">The token after <c>Bearer</c>.</param>
    /// <param name="request">The request the field arrived on; not read.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Accepted as the empty-name account, or refused.</returns>
    public ValueTask<HttpCredentialCheck> VerifyAsync(
        string credentials, HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        cancellationToken.ThrowIfCancellationRequested();

        var matches = accounts.CheckBearerToken(Encoding.Latin1.GetBytes(credentials));

        return ValueTask.FromResult(matches && credentials.Length > 0 ? Accepted : Refused);
    }
}
