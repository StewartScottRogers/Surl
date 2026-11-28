namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IAuthenticationPolicy"/>, <see cref="IMailAuthenticationPolicy"/>,
/// <see cref="ISshAuthenticationPolicy"/> and <see cref="ISmbAuthenticationPolicy"/> that lets
/// everyone in (ADR-0032, section 6; ADR-0049, section 6; ADR-0051, section 7; ADR-0073,
/// decision 3): every password login is
/// <see cref="PasswordLoginVerdict.AcceptedUnchecked"/>, every HTTP request
/// <see cref="HttpAuthenticationOutcome.Proceed"/>s with no <c>WWW-Authenticate</c> values and
/// no account, and every SASL login ends <see cref="SaslLoginOutcome.AcceptedUnchecked"/>, with no
/// security layer - much as <c>Surl.Authentication</c>'s policy answers under
/// <c>--allow-anonymous</c>, though that one still checks some logins, such as an S3 request's
/// signature. Every scheme is offered the SASL mechanism <c>PLAIN</c> alone, a mail server the
/// clear-password login too but not <c>APOP</c>, and any SASL mechanism is accepted on its initial
/// response, or on whatever answers one empty challenge when none was sent. Every SSH
/// <c>none</c>, password and signed public-key login is
/// <see cref="SshLoginOutcome.AcceptedUnchecked"/> and every public-key query
/// <see cref="SshLoginOutcome.KeyAcceptable"/>, with no note, so upstream curl completes its login
/// in one <c>none</c> request. Every SMB NTLMv1 session setup is
/// <see cref="SmbLoginOutcome.AcceptedUnchecked"/>, with no note. Protocol tests share it as a
/// test double. In production code it is the policy <c>HttpProxyProtocolServer</c>'s one-argument
/// constructor passes; the initial value of <c>XmppServerOptions.AuthenticationPolicy</c> and of
/// <c>Surl.Composition</c>'s <c>ComposedAuthentication.ProxyPolicy</c> and
/// <c>ComposedProtocolServices.ProxyAuthenticationPolicy</c>; what <c>Surl.Composition</c>'s
/// <c>NoAccountProxyAuthenticationPolicy</c> answers password logins and HTTP sessions with; and
/// the policy type the SMTP, IMAP and POP3 upstream test case servers' constructors take.
/// </summary>
public sealed class AnonymousAuthenticationPolicy :
    IAuthenticationPolicy, IMailAuthenticationPolicy, ISshAuthenticationPolicy, ISmbAuthenticationPolicy
{
    private static readonly SshLoginVerdict SshAcceptUnchecked = new(SshLoginOutcome.AcceptedUnchecked, null, null);

    private static readonly SshLoginVerdict SshKeyAcceptable = new(SshLoginOutcome.KeyAcceptable, null, null);

    private static readonly SmbLoginVerdict SmbAcceptUnchecked = new(SmbLoginOutcome.AcceptedUnchecked, null, null);

    private static readonly HttpAuthenticationVerdict ProceedAnonymously =
        new(HttpAuthenticationOutcome.Proceed, [], null);

    private static readonly MailLoginOffer PlainAndClearPasswordOffer = new(["PLAIN"], true, false);

    private static readonly SaslLoginStep AcceptUnchecked =
        new(SaslLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null);

    private static readonly SaslLoginStep ChallengeEmpty =
        new(SaslLoginOutcome.Challenge, ReadOnlyMemory<byte>.Empty, null, null);

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
    public IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return PlainAndClearPasswordOffer.SaslMechanisms;
    }

    /// <inheritdoc/>
    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        ArgumentNullException.ThrowIfNull(start);

        return new AnonymousSaslExchange(start.InitialResponse.HasValue);
    }

    /// <inheritdoc/>
    public ValueTask<SaslLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken)
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

    /// <inheritdoc/>
    public ValueTask<SmbLoginVerdict> CheckSmbNtlmV1LoginAsync(
        SmbNtlmV1Login login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(SmbAcceptUnchecked);
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

        public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken)
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

        public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
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
