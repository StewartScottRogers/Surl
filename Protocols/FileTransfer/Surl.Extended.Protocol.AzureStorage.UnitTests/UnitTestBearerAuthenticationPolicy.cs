using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// A hand-written policy for the server's tests, judging as <c>Surl.Authentication</c>'s HTTP
/// session does for an Azure Storage request with a bearer-only account set: <c>Bearer
/// secret-token</c> proceeds as <see cref="AccountName"/> (the empty name, as the bearer method
/// answers), another bearer token is forbidden as <c>UnknownAccount</c>, any other credential as
/// <c>UnsupportedScheme</c>, and a request without one proceeds anonymously only when
/// <paramref name="allowsAnonymous"/> - writes included, as the real session does - and is
/// forbidden as <c>MissingCredentials</c> otherwise. <see cref="Verdict"/>, when set, answers
/// every request instead, as a Shared Key or shared access signature check would.
/// </summary>
internal sealed class UnitTestBearerAuthenticationPolicy(bool allowsAnonymous) : IAuthenticationPolicy
{
    public const string Token = "secret-token";

    public bool AllowsAnonymous { get; } = allowsAnonymous;

    public string AccountName { get; init; } = string.Empty;

    public List<HttpAuthenticationRequest> Requests { get; } = [];

    public HttpAuthenticationVerdict? Verdict { get; set; }

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
            if (policy.Verdict is { } scripted)
            {
                return ValueTask.FromResult(scripted);
            }

            var authorization = request.Fields.FirstOrDefault(field => field.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)).Value;
            HttpAuthenticationVerdict verdict = authorization switch
            {
                null when policy.AllowsAnonymous => new(HttpAuthenticationOutcome.Proceed, [], null),
                null => Forbidden(HttpAuthenticationRefusal.MissingCredentials),
                "Bearer " + Token => new(HttpAuthenticationOutcome.Proceed, [], policy.AccountName),
                _ when authorization.StartsWith("Bearer ", StringComparison.Ordinal) && authorization[7..].Trim().Length > 0 => Forbidden(HttpAuthenticationRefusal.UnknownAccount),
                _ => Forbidden(HttpAuthenticationRefusal.UnsupportedScheme),
            };

            return ValueTask.FromResult(verdict);
        }

        private static HttpAuthenticationVerdict Forbidden(HttpAuthenticationRefusal refusal) =>
            new(HttpAuthenticationOutcome.Forbidden, [], null, Refusal: refusal);
    }
}
