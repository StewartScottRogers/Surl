using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// An <see cref="IAuthenticationPolicy"/> with one account, <c>user</c> / <c>pass</c>, judging HTTP
/// requests as ADR-0032 section 4 says a Basic login is: the account's credentials proceed, other
/// Basic credentials are forbidden, and a request without credentials is challenged for Basic and
/// NTLM - or, with <paramref name="allowAnonymous"/> (<c>--allow-anonymous</c>), proceeds.
/// </summary>
/// <param name="allowAnonymous">Whether a request without credentials proceeds.</param>
internal sealed class UnitTestAuthenticationPolicy(bool allowAnonymous) : IAuthenticationPolicy, IHttpAuthenticationSession
{
    /// <summary>The <c>authorization</c> value of the one account: Basic <c>user:pass</c>.</summary>
    public const string GoodAuthorization = "Basic dXNlcjpwYXNz";

    /// <summary>The challenges a request without credentials earns.</summary>
    public static readonly string[] Challenges = ["Basic realm=\"surl\"", "NTLM"];

    /// <summary>The requests judged, in order.</summary>
    public List<HttpAuthenticationRequest> Requests { get; } = [];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException("WebTransport judges HTTP requests only.");

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => this;

    public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request);
        }

        var authorization = request.Fields.FirstOrDefault(field => field.Key == "authorization").Value;
        var verdict = authorization switch
        {
            GoodAuthorization => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "user"),
            null when allowAnonymous => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null),
            null => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, Challenges, null),
            _ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Forbidden, [], null),
        };
        return ValueTask.FromResult(verdict);
    }
}
