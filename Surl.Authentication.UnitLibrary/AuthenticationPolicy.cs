using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The <see cref="IAuthenticationPolicy"/> <c>surl</c> composes (ADR-0032, sections 4, 5 and 8):
/// with no accounts every login is refused, anonymous access only where ADR-0032 allows it, a
/// plain-text secret on an unencrypted connection refused unchecked unless
/// <c>--allow-plaintext-auth</c>, only the accepted methods offered and checked, and every
/// refused credential answered after <see cref="RefusalDelay"/> on the injected
/// <see cref="TimeProvider"/>. As the <see cref="IMailAuthenticationPolicy"/> it offers and runs
/// the SASL mechanisms <c>--auth</c> accepts (ADR-0049, sections 2 and 5): today
/// <c>DIGEST-MD5</c>, <c>CRAM-MD5</c> and <c>NTLM</c>, offered on any connection, and <c>PLAIN</c>,
/// <c>LOGIN</c>, <c>XOAUTH2</c> and <c>OAUTHBEARER</c>, all plain-text, so offered and run only over
/// TLS or with <c>--allow-plaintext-auth</c>; and POP3 <c>APOP</c> when <c>--auth</c> accepts it.
/// </summary>
public sealed class AuthenticationPolicy : IAuthenticationPolicy, IMailAuthenticationPolicy
{
    private const string ApopMethod = "APOP";

    private static readonly MailLoginStep RefusedApop =
        new(MailLoginOutcome.RefusedMechanism, ReadOnlyMemory<byte>.Empty, null, null);

    private static readonly MailLoginStep AcceptedApopUnchecked =
        new(MailLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null);

    // The random part of a CRAM-MD5 challenge: 16 hex digits (ADR-0049, section 5).
    private const int TimestampNonceLength = 8;

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
        : this(settings, httpMethods, timeProvider, RandomSaslNonceSource.Instance, RandomNtlmServerChallengeSource.Instance)
    {
    }

    /// <summary>
    /// As the public constructor, with the SASL challenges' random bytes from
    /// <paramref name="nonceSource"/> and SASL <c>NTLM</c>'s server challenges from
    /// <paramref name="ntlmServerChallenges"/>, so tests can check answers measured from upstream curl.
    /// </summary>
    /// <param name="settings">The accounts and loosening options.</param>
    /// <param name="httpMethods">Every HTTP method this build implements; at most one per method.</param>
    /// <param name="timeProvider">The clock the refusal delay waits on and <c>CRAM-MD5</c> challenges carry.</param>
    /// <param name="nonceSource">Where <c>CRAM-MD5</c> and <c>DIGEST-MD5</c> challenges' random bytes come from.</param>
    /// <param name="ntlmServerChallenges">Where SASL <c>NTLM</c>'s server challenges come from.</param>
    internal AuthenticationPolicy(
        AuthenticationSettings settings,
        IEnumerable<IHttpAuthenticationMethod> httpMethods,
        TimeProvider timeProvider,
        ISaslNonceSource nonceSource,
        INtlmServerChallengeSource ntlmServerChallenges)
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
        NonceSource = nonceSource;
        NtlmServerChallenges = ntlmServerChallenges;
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
    /// <c>APOP</c> whenever <c>--auth</c> accepts it, since it sends no plain-text secret. It does
    /// not depend on whether any account is configured.
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
            settings.AcceptedMethods.Contains(AuthenticationMethod.Apop));
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
    /// POP3 <c>APOP</c>, RFC 1939 section 7 (ADR-0049, sections 5 and 7):
    /// <see cref="MailLoginOutcome.RefusedMechanism"/> when <c>--auth</c> does not accept it,
    /// <see cref="MailLoginOutcome.AcceptedUnchecked"/> under <c>--allow-anonymous</c>, and otherwise
    /// the digest checked as 32 hex digits of MD5 over the timestamp's bytes then the password's
    /// UTF-8 bytes, compared in fixed time; a refusal is answered after <see cref="RefusalDelay"/>.
    /// </summary>
    /// <param name="login">The login, with the timestamp this connection's greeting carried.</param>
    /// <param name="cancellationToken">Cancels the check and the refusal delay.</param>
    /// <returns>How the login ended; never a challenge.</returns>
    public async ValueTask<MailLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        if (!settings.AcceptedMethods.Contains(AuthenticationMethod.Apop))
        {
            return RefusedApop;
        }

        if (settings.AllowAnonymous)
        {
            return AcceptedApopUnchecked;
        }

        var user = string.IsNullOrEmpty(login.UserName) ? null : login.UserName;
        var account = settings.Accounts.FindChallengeResponseAccount(user);
        byte[] digested = [.. Encoding.UTF8.GetBytes(login.Timestamp), .. account.Password];
        if (Md5HexDigest.Matches(settings.Accounts.SecretComparer, MD5.HashData(digested), login.Digest) & account.AccountName is not null)
        {
            return new MailLoginStep(
                MailLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, account.AccountName, new CheckedLogin(ApopMethod, user, true));
        }

        await WaitRefusalDelayAsync(cancellationToken).ConfigureAwait(false);

        return new MailLoginStep(
            MailLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, new CheckedLogin(ApopMethod, user, false));
    }

    internal AuthenticationSettings Settings => settings;

    internal ISaslNonceSource NonceSource { get; }

    internal INtlmServerChallengeSource NtlmServerChallenges { get; }

    /// <summary>
    /// A <c>CRAM-MD5</c> challenge, RFC 2195's <c>msg-id</c> form: <c>&lt;</c>, 16 lower-case hex
    /// digits of random, <c>.</c>, the Unix time in seconds, <c>@surl&gt;</c> (ADR-0049, section 5).
    /// </summary>
    /// <returns>The challenge.</returns>
    internal string CreateTimestamp() =>
        $"<{Convert.ToHexStringLower(NonceSource.CreateNonce(TimestampNonceLength))}.{timeProvider.GetUtcNow().ToUnixTimeSeconds()}@surl>";

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
