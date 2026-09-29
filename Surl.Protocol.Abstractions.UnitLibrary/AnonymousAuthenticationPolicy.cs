namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IAuthenticationPolicy"/> that lets everyone in (ADR-0032, section 6): every
/// password login is <see cref="PasswordLoginVerdict.Accepted"/> and every HTTP request
/// <see cref="HttpAuthenticationOutcome.Proceed"/>s with no <c>WWW-Authenticate</c> values and
/// no account - the behaviour of <c>--allow-anonymous</c>. It is the test double protocol tests
/// share, and the policy a server's policy-less constructor passes until BL-117 composes the
/// real one.
/// </summary>
public sealed class AnonymousAuthenticationPolicy : IAuthenticationPolicy
{
    private static readonly HttpAuthenticationVerdict ProceedAnonymously =
        new(HttpAuthenticationOutcome.Proceed, [], null);

    private readonly AnonymousHttpAuthenticationSession session = new();

    /// <inheritdoc/>
    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(
        PasswordLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(PasswordLoginVerdict.Accepted);
    }

    /// <inheritdoc/>
    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => session;

    private sealed class AnonymousHttpAuthenticationSession : IHttpAuthenticationSession
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(
            HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(ProceedAnonymously);
        }
    }
}
