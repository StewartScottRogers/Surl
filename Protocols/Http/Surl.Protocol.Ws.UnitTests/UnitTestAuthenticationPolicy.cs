using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// A hand-written <see cref="IAuthenticationPolicy"/> for the WebSocket server's tests: every
/// connection's session answers each request with the verdict <c>judge</c> returns, and the
/// policy records the <see cref="TlsSession"/> each connection was started with and every
/// request it was shown.
/// </summary>
internal sealed class UnitTestAuthenticationPolicy(Func<HttpAuthenticationRequest, HttpAuthenticationVerdict> judge) : IAuthenticationPolicy
{
    private readonly List<TlsSession?> startedTlsSessions = [];
    private readonly List<HttpAuthenticationRequest> judgedRequests = [];

    private HttpAuthenticationVerdict Judge(HttpAuthenticationRequest request) => judge(request);

    public IReadOnlyList<TlsSession?> StartedTlsSessions => startedTlsSessions;

    public IReadOnlyList<HttpAuthenticationRequest> JudgedRequests => judgedRequests;

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The WebSocket server never checks a password login.");

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession)
    {
        startedTlsSessions.Add(tlsSession);

        return new Session(this);
    }

    private sealed class Session(UnitTestAuthenticationPolicy policy) : IHttpAuthenticationSession
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            policy.judgedRequests.Add(request);

            return ValueTask.FromResult(policy.Judge(request));
        }
    }
}
