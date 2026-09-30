using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The <see cref="IAuthenticationPolicy"/> <c>surl</c> composes (ADR-0032, sections 4, 5 and 8):
/// with no accounts every login is refused, anonymous access only where ADR-0032 allows it, a
/// plain-text secret on an unencrypted connection refused unchecked unless
/// <c>--allow-plaintext-auth</c>, only the accepted methods offered and checked, and every
/// refused credential answered after <see cref="RefusalDelay"/> on the injected
/// <see cref="TimeProvider"/>. As the <see cref="IMailAuthenticationPolicy"/> it offers and runs
/// the SASL mechanisms <c>--auth</c> accepts (ADR-0049, sections 2 and 5): today <c>PLAIN</c>,
/// <c>LOGIN</c>, <c>XOAUTH2</c> and <c>OAUTHBEARER</c>, all plain-text, so offered and run only over
/// TLS or with <c>--allow-plaintext-auth</c>; <c>APOP</c> is not offered yet.
/// </summary>
public sealed class AuthenticationPolicy : IAuthenticationPolicy, IMailAuthenticationPolicy
{
    private static readonly MailLoginStep RefusedApop =
        new(MailLoginOutcome.RefusedMechanism, ReadOnlyMemory<byte>.Empty, null, null);

    /// <summary>
    /// How long a refused credential waits before it is answered (ADR-0032, section 8).
    /// </summary>
    public static readonly TimeSpan RefusalDelay = TimeSpan.FromSeconds(1);

    private readonly AuthenticationSettings settings;
    private readonly IReadOnlyList<IHttpAuthenticationMethod> httpMethods;
    private readonly TimeProvider timeProvider;
    private readonly IReadOnlyList<SaslMechanism> saslMechanisms;

    /// <summary>
    /// Applies <paramref name="settings"/>, verifying HTTP credentials with those of
    /// <paramref name="httpMethods"/> that <see cref="AuthenticationSettings.AcceptedMethods"/> names.
    /// </summary>
    /// <param name="settings">The accounts and loosening options.</param>
    /// <param name="httpMethods">Every HTTP method this build implements; at most one per method.</param>
    /// <param name="timeProvider">The clock the refusal delay waits on.</param>
    /// <exception cref="ArgumentException">Two of <paramref name="httpMethods"/> verify the same method.</exception>
    public AuthenticationPolicy(
        AuthenticationSettings settings,
        IEnumerable<IHttpAuthenticationMethod> httpMethods,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpMethods);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(settings.AcceptedMethods, nameof(settings));

        var implemented = httpMethods.ToList();
        if (implemented.DistinctBy(method => method.Method).Count() != implemented.Count)
        {
            throw new ArgumentException("An HTTP authentication method is given twice.", nameof(httpMethods));
        }

        this.settings = settings;
        this.httpMethods = [.. implemented
            .Where(method => settings.AcceptedMethods.Contains(method.Method))
            .OrderBy(method => method.Method)];
        this.timeProvider = timeProvider;
        saslMechanisms = [.. SaslMechanism.InOfferOrder.Where(mechanism => settings.AcceptedMethods.Contains(mechanism.Method))];
    }

    /// <inheritdoc/>
    public async ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(
        PasswordLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        var verdict = JudgePasswordLogin(login);
        if (verdict == PasswordLoginVerdict.RefusedCredentials)
        {
            await WaitRefusalDelayAsync(cancellationToken).ConfigureAwait(false);
        }

        return verdict;
    }

    /// <inheritdoc/>
    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) =>
        new HttpAuthenticationSession(this, tlsSession is not null);

    /// <summary>
    /// The accepted SASL mechanisms that may be used on a connection in this TLS state, in
    /// ADR-0049 section 2's order; the clear-password login when a plain-text secret may be sent;
    /// never <c>APOP</c> yet. It does not depend on whether any account is configured.
    /// </summary>
    /// <param name="tlsSession">The connection's TLS session; <see langword="null"/> means unencrypted.</param>
    /// <returns>What the mail server advertises.</returns>
    public MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession)
    {
        var isEncrypted = tlsSession is not null;

        return new MailLoginOffer(
            [.. saslMechanisms
                .Where(mechanism => MayUseSaslMechanism(mechanism, isEncrypted))
                .Select(mechanism => mechanism.Name)],
            OffersPlaintextSecrets(isEncrypted),
            false);
    }

    /// <summary>
    /// Starts the exchange of the mechanism <paramref name="start"/> names, matched
    /// case-insensitively: refused as <see cref="MailLoginOutcome.RefusedMechanism"/> when it is
    /// unknown or not accepted, and as <see cref="MailLoginOutcome.RefusedPlaintext"/> when it is
    /// plain-text on an unencrypted connection without <c>--allow-plaintext-auth</c> or
    /// <c>--allow-anonymous</c> (ADR-0049, sections 1 and 5).
    /// </summary>
    /// <param name="start">How the client started.</param>
    /// <returns>The exchange.</returns>
    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        ArgumentNullException.ThrowIfNull(start);

        var mechanism = saslMechanisms.FirstOrDefault(
            mechanism => string.Equals(mechanism.Name, start.Mechanism, StringComparison.OrdinalIgnoreCase));
        if (mechanism is null)
        {
            return new RefusedSaslExchange(MailLoginOutcome.RefusedMechanism);
        }

        return settings.AllowAnonymous || MayUseSaslMechanism(mechanism, start.TlsSession is not null)
            ? mechanism.Start(new SaslExchangeContext(this, mechanism.Name, start.InitialResponse))
            : new RefusedSaslExchange(MailLoginOutcome.RefusedPlaintext);
    }

    // A mechanism that sends no plain-text secret may be used on any connection (ADR-0049, section 1).
    private bool MayUseSaslMechanism(SaslMechanism mechanism, bool isEncrypted) =>
        OffersPlaintextSecrets(isEncrypted || !AuthenticationMethods.SendsPlaintextSecret(mechanism.Method));

    /// <summary>
    /// POP3 <c>APOP</c>, which is not offered until BL-195 builds it: always
    /// <see cref="MailLoginOutcome.RefusedMechanism"/> (ADR-0049, section 7).
    /// </summary>
    /// <param name="login">The login.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The refusal.</returns>
    public ValueTask<MailLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(RefusedApop);
    }

    internal AuthenticationSettings Settings => settings;

    internal IReadOnlyList<IHttpAuthenticationMethod> HttpMethods => httpMethods;

    internal Task WaitRefusalDelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(RefusalDelay, timeProvider, cancellationToken);

    internal bool OffersPlaintextSecrets(bool isEncrypted) => isEncrypted || settings.AllowPlaintextAuthentication;

    private PasswordLoginVerdict JudgePasswordLogin(PasswordLogin login)
    {
        if (settings.AllowAnonymous)
        {
            return PasswordLoginVerdict.AcceptedUnchecked;
        }

        if (login.Password is not null && !OffersPlaintextSecrets(login.TlsSession is not null))
        {
            return PasswordLoginVerdict.RefusedPlaintext;
        }

        if (login.UserName is null)
        {
            return PasswordLoginVerdict.RefusedAnonymous;
        }

        return settings.Accounts.CheckPassword(login.UserName, login.Password.GetValueOrDefault().Span)
            ? PasswordLoginVerdict.Accepted
            : PasswordLoginVerdict.RefusedCredentials;
    }
}
