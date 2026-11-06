using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// An authentication policy whose sessions never give a verdict until the request's token is
/// cancelled, so a request stream stays open with its body unread for as long as a test needs.
/// </summary>
internal sealed class UnitTestStallingAuthenticationPolicy : IAuthenticationPolicy
{
    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The HTTP server never checks a password login.");

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => new Session();

    private sealed class Session : IHttpAuthenticationSession
    {
        public async ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            throw new InvalidOperationException("An infinite delay ended without its cancellation.");
        }
    }
}
