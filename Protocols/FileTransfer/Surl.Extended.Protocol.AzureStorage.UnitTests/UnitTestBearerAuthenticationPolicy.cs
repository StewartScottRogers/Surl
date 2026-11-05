using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// A hand-written policy for the server's tests, judging as <c>Surl.Authentication</c>'s HTTP
/// session does for a bearer-only account set: <c>Bearer secret-token</c> proceeds as
/// <see cref="AccountName"/> (the empty name, as the bearer method answers), any other credential
/// is challenged, and a request without one proceeds anonymously only when
/// <paramref name="allowsAnonymous"/> - writes included, as the real session does.
/// </summary>
internal sealed class UnitTestBearerAuthenticationPolicy(bool allowsAnonymous) : IAuthenticationPolicy
{
    public const string Token = "secret-token";

    public bool AllowsAnonymous { get; } = allowsAnonymous;

    public string AccountName { get; init; } = string.Empty;

    public List<HttpAuthenticationRequest> Requests { get; } = [];

    public List<TlsSession?> StartedConnections { get; } = [];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession)
    {
        StartedConnections.Add(tlsSession);

        return new Session(this);
    }

    private sealed class Session(UnitTestBearerAuthenticationPolicy policy) : IHttpAuthenticationSession
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            policy.Requests.Add(request);
            var authorization = request.Fields.FirstOrDefault(field => field.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)).Value;
            HttpAuthenticationVerdict verdict = authorization switch
            {
                null when policy.AllowsAnonymous => new(HttpAuthenticationOutcome.Proceed, [], null),
                "Bearer " + Token => new(HttpAuthenticationOutcome.Proceed, [], policy.AccountName),
                _ => new(HttpAuthenticationOutcome.Challenge, ["Bearer realm=\"surl\""], null),
            };

            return ValueTask.FromResult(verdict);
        }
    }
}
