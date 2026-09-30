namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IAuthenticationPolicy"/>, <see cref="IMailAuthenticationPolicy"/> and
/// <see cref="ISshAuthenticationPolicy"/> that lets everyone in (ADR-0032, section 6; ADR-0049,
/// section 6; ADR-0051, section 7): every password login is
/// <see cref="PasswordLoginVerdict.AcceptedUnchecked"/>, every HTTP request
/// <see cref="HttpAuthenticationOutcome.Proceed"/>s with no <c>WWW-Authenticate</c> values and
/// no account, and every mail login ends <see cref="MailLoginOutcome.AcceptedUnchecked"/> - the
/// behaviour of <c>--allow-anonymous</c>. A mail server is offered <c>PLAIN</c> and the
/// clear-password login but not <c>APOP</c>, and any SASL mechanism is accepted on its initial
/// response, or on whatever answers one empty challenge when none was sent. Every SSH
/// <c>none</c>, password and signed public-key login is
/// <see cref="SshLoginOutcome.AcceptedUnchecked"/> and every public-key query
/// <see cref="SshLoginOutcome.KeyAcceptable"/>, with no note, so upstream curl completes its login
/// in one <c>none</c> request. It is the test double protocol tests share, and the policy a
/// server's policy-less constructor passes until BL-117 composes the real one.
/// </summary>
public sealed class AnonymousAuthenticationPolicy :
    IAuthenticationPolicy, IMailAuthenticationPolicy, ISshAuthenticationPolicy
{
    private static readonly SshLoginVerdict SshAcceptUnchecked = new(SshLoginOutcome.AcceptedUnchecked, null, null);

    private static readonly SshLoginVerdict SshKeyAcceptable = new(SshLoginOutcome.KeyAcceptable, null, null);

    private static readonly HttpAuthenticationVerdict ProceedAnonymously =
        new(HttpAuthenticationOutcome.Proceed, [], null);

    private static readonly MailLoginOffer PlainAndClearPasswordOffer = new(["PLAIN"], true, false);

    private static readonly MailLoginStep AcceptUnchecked =
        new(MailLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null);

    private static readonly MailLoginStep ChallengeEmpty =
        new(MailLoginOutcome.Challenge, ReadOnlyMemory<byte>.Empty, null, null);

    private readonly AnonymousHttpAuthenticationSession session = new();

    /// <inheritdoc/>
    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(
        PasswordLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(PasswordLoginVerdict.AcceptedUnchecked);
    }

    /// <inheritdoc/>
    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => session;

    /// <inheritdoc/>
    public MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession) => PlainAndClearPasswordOffer;

    /// <inheritdoc/>
    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        ArgumentNullException.ThrowIfNull(start);

        return new AnonymousSaslExchange(start.InitialResponse.HasValue);
    }

    /// <inheritdoc/>
    public ValueTask<MailLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(AcceptUnchecked);
    }

    /// <inheritdoc/>
    public SshLoginVerdict CheckSshNoneLogin(SshNoneLogin login)
    {
        ArgumentNullException.ThrowIfNull(login);

        return SshAcceptUnchecked;
    }

    /// <inheritdoc/>
    public ValueTask<SshLoginVerdict> CheckSshPasswordLoginAsync(
        SshPasswordLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(SshAcceptUnchecked);
    }

    /// <inheritdoc/>
    public ValueTask<SshLoginVerdict> CheckSshPublicKeyLoginAsync(
        SshPublicKeyLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(login.Proof == SshPublicKeyProof.None ? SshKeyAcceptable : SshAcceptUnchecked);
    }

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

    private sealed class AnonymousSaslExchange(bool hasInitialResponse) : ISaslExchange
    {
        private bool isBegun;
        private bool isAwaitingResponse;

        public ValueTask<MailLoginStep> BeginAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isBegun)
            {
                throw new InvalidOperationException("The SASL exchange has already begun.");
            }

            isBegun = true;
            isAwaitingResponse = !hasInitialResponse;
            return ValueTask.FromResult(hasInitialResponse ? AcceptUnchecked : ChallengeEmpty);
        }

        public ValueTask<MailLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!isAwaitingResponse)
            {
                throw new InvalidOperationException("The SASL exchange's last step was not a challenge.");
            }

            isAwaitingResponse = false;
            return ValueTask.FromResult(AcceptUnchecked);
        }
    }
}
