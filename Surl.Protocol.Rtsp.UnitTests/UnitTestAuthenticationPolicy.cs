using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// A hand-written <see cref="IAuthenticationPolicy"/> for the RTSP server's tests: every
/// connection's session answers each request with the verdict <c>judge</c> returns for the
/// connection's <see cref="TlsSession"/> and the request, and the policy records the
/// <see cref="TlsSession"/> each connection was started with and every request it was shown.
/// </summary>
internal sealed class UnitTestAuthenticationPolicy(Func<TlsSession?, HttpAuthenticationRequest, HttpAuthenticationVerdict> judge) : IAuthenticationPolicy
{
    private readonly List<TlsSession?> startedTlsSessions = [];
    private readonly List<HttpAuthenticationRequest> judgedRequests = [];

    public UnitTestAuthenticationPolicy(Func<HttpAuthenticationRequest, HttpAuthenticationVerdict> judge)
        : this((_, request) => judge(request))
    {
    }

    private HttpAuthenticationVerdict Judge(TlsSession? tlsSession, HttpAuthenticationRequest request) => judge(tlsSession, request);

    public IReadOnlyList<TlsSession?> StartedTlsSessions => startedTlsSessions;

    public IReadOnlyList<HttpAuthenticationRequest> JudgedRequests => judgedRequests;

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The RTSP server never checks a password login.");

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession)
    {
        startedTlsSessions.Add(tlsSession);

        return new Session(this, tlsSession);
    }

    private sealed class Session(UnitTestAuthenticationPolicy policy, TlsSession? tlsSession) : IHttpAuthenticationSession
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            policy.judgedRequests.Add(request);

            return ValueTask.FromResult(policy.Judge(tlsSession, request));
        }
    }
}
