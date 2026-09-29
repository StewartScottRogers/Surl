using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The <see cref="IAuthenticationPolicy"/> <c>surl</c> composes (ADR-0032, sections 4, 5 and 8):
/// with no accounts every login is refused, anonymous access only where ADR-0032 allows it, a
/// plain-text secret on an unencrypted connection refused unchecked unless
/// <c>--allow-plaintext-auth</c>, only the accepted methods offered and checked, and every
/// refused credential answered after <see cref="RefusalDelay"/> on the injected
/// <see cref="TimeProvider"/>.
/// </summary>
public sealed class AuthenticationPolicy : IAuthenticationPolicy
{
    /// <summary>
    /// How long a refused credential waits before it is answered (ADR-0032, section 8).
    /// </summary>
    public static readonly TimeSpan RefusalDelay = TimeSpan.FromSeconds(1);

    private readonly AuthenticationSettings settings;
    private readonly IReadOnlyList<IHttpAuthenticationMethod> httpMethods;
    private readonly TimeProvider timeProvider;

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

    internal AuthenticationSettings Settings => settings;

    internal IReadOnlyList<IHttpAuthenticationMethod> HttpMethods => httpMethods;

    internal Task WaitRefusalDelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(RefusalDelay, timeProvider, cancellationToken);

    internal bool OffersPlaintextSecrets(bool isEncrypted) => isEncrypted || settings.AllowPlaintextAuth;

    private PasswordLoginVerdict JudgePasswordLogin(PasswordLogin login)
    {
        if (settings.AllowAnonymous)
        {
            return PasswordLoginVerdict.Accepted;
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
