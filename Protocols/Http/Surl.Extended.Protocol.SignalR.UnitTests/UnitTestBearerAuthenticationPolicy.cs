using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A hand-written policy for the server's tests, judging as <c>Surl.Authentication</c>'s HTTP
/// session does for a bearer-only account set: <c>Bearer secret-token</c> proceeds as the empty
/// account name, <c>Bearer forbidden-token</c> is forbidden, any other credential is challenged, and a
/// request without one proceeds anonymously only when <paramref name="allowsAnonymous"/>.
/// </summary>
internal sealed class UnitTestBearerAuthenticationPolicy(bool allowsAnonymous) : IAuthenticationPolicy
{
    public const string Token = "secret-token";

    public const string ForbiddenToken = "forbidden-token";

    public const string Challenge = "Bearer realm=\"surl\"";

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
                "Bearer " + Token => new(HttpAuthenticationOutcome.Proceed, [], string.Empty),
                "Bearer " + ForbiddenToken => new(HttpAuthenticationOutcome.Forbidden, [], null),
                _ => new(HttpAuthenticationOutcome.Challenge, [Challenge], null),
            };

            return ValueTask.FromResult(verdict);
        }

    }

    public bool AllowsAnonymous { get; } = allowsAnonymous;
}
