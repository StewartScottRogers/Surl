using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// A hand-written policy for the transport's tests, judging as <c>Surl.Authentication</c>'s HTTP session does
/// for Bearer-only accounts: <c>Bearer secret-token</c> proceeds as the empty-name account the Bearer method
/// answers, <c>Bearer alice-token</c> as <c>alice</c>, any other credential is challenged, and a request without
/// one proceeds anonymously only when <paramref name="allowsAnonymous" />.
/// </summary>
internal sealed class UnitTestBearerAuthenticationPolicy(bool allowsAnonymous) : IAuthenticationPolicy
{
    public const string Token = "secret-token";

    public const string AliceToken = "alice-token";

    public List<TlsSession?> StartedConnections { get; } = [];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession)
    {
        StartedConnections.Add(tlsSession);
        return new Session(allowsAnonymous);
    }

    private sealed class Session(bool allowsAnonymous) : IHttpAuthenticationSession
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            string? authorization = request.Fields.FirstOrDefault(field => field.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)).Value;
            HttpAuthenticationVerdict verdict = authorization switch
            {
                null when allowsAnonymous => new(HttpAuthenticationOutcome.Proceed, [], null),
                "Bearer " + Token => new(HttpAuthenticationOutcome.Proceed, [], string.Empty),
                "Bearer " + AliceToken => new(HttpAuthenticationOutcome.Proceed, [], "alice"),
                _ => new(HttpAuthenticationOutcome.Challenge, ["Bearer realm=\"surl\""], null),
            };

            return ValueTask.FromResult(verdict);
        }
    }
}
