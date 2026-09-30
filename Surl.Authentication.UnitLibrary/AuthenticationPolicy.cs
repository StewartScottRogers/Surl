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
/// TLS or with <c>--allow-plaintext-auth</c>; <c>EXTERNAL</c>, offered and run only on a
/// connection with a verified TLS client certificate; and POP3 <c>APOP</c> when <c>--auth</c> accepts it.
/// As the <see cref="ISshAuthenticationPolicy"/> it checks SSH passwords against the accounts,
/// never refused as plain-text since SSH encrypts first, and public keys against
/// <see cref="AuthenticationSettings.AuthorizedKeys"/> (ADR-0051, sections 6 and 7).
/// </summary>
public sealed class AuthenticationPolicy : IAuthenticationPolicy, IMailAuthenticationPolicy, ISshAuthenticationPolicy
{
    private const string ApopMethod = "APOP";

    private const string SshPublicKeyMethod = "publickey";

    private static readonly SshLoginVerdict SshRefusedUnchecked = new(SshLoginOutcome.Refused, null, null);

    private static readonly SshLoginVerdict SshAcceptedUnchecked = new(SshLoginOutcome.AcceptedUnchecked, null, null);

    private static readonly SshLoginVerdict SshKeyAcceptable = new(SshLoginOutcome.KeyAcceptable, null, null);

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
                .Where(mechanism => MayUseSaslMechanism(mechanism, isEncrypted) && CanIdentifyClient(mechanism, tlsSession))
                .Select(mechanism => mechanism.Name)],
            OffersPlaintextSecrets(isEncrypted),
            settings.AcceptedMethods.Contains(AuthenticationMethod.Apop));
    }

    /// <summary>
    /// Starts the exchange of the mechanism <paramref name="start"/> names, matched
    /// case-insensitively: refused as <see cref="MailLoginOutcome.RefusedMechanism"/> when it is
    /// unknown, not accepted, or <c>EXTERNAL</c> on a connection with no TLS client certificate,
    /// and as <see cref="MailLoginOutcome.RefusedPlaintext"/> when it is
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
        if (mechanism is null || !CanIdentifyClient(mechanism, start.TlsSession))
        {
            return new RefusedSaslExchange(MailLoginOutcome.RefusedMechanism);
        }

        return settings.AllowAnonymous || MayUseSaslMechanism(mechanism, start.TlsSession is not null)
            ? mechanism.Start(new SaslExchangeContext(this, mechanism.Name, start.InitialResponse, start.TlsSession?.ClientCertificate))
            : new RefusedSaslExchange(MailLoginOutcome.RefusedPlaintext);
    }

    // A mechanism that sends no plain-text secret may be used on any connection (ADR-0049, section 1).
    private bool MayUseSaslMechanism(SaslMechanism mechanism, bool isEncrypted) =>
        OffersPlaintextSecrets(isEncrypted || !AuthenticationMethods.SendsPlaintextSecret(mechanism.Method));

    // EXTERNAL's client is its TLS client certificate, so it needs one, even under --allow-anonymous
    // (ADR-0049, sections 2 and 4).
    private static bool CanIdentifyClient(SaslMechanism mechanism, TlsSession? tlsSession) =>
        mechanism.Method != AuthenticationMethod.External || tlsSession?.ClientCertificate is not null;

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

    /// <summary>
    /// An SSH <c>none</c> request (ADR-0051, section 6): <see cref="SshLoginOutcome.AcceptedUnchecked"/>
    /// under <c>--allow-anonymous</c>, otherwise <see cref="SshLoginOutcome.Refused"/>; never
    /// delayed, never noted.
    /// </summary>
    /// <param name="login">The request as the client sent it.</param>
    /// <returns>Whether the client is logged in without a credential.</returns>
    public SshLoginVerdict CheckSshNoneLogin(SshNoneLogin login)
    {
        ArgumentNullException.ThrowIfNull(login);

        return settings.AllowAnonymous ? SshAcceptedUnchecked : SshRefusedUnchecked;
    }

    /// <summary>
    /// An SSH <c>password</c> request or <c>keyboard-interactive</c> answer (ADR-0051, section 6):
    /// <see cref="SshLoginOutcome.AcceptedUnchecked"/> under <c>--allow-anonymous</c>; otherwise
    /// checked against the named accounts whatever <c>--allow-plaintext-auth</c> says, and a
    /// refusal - wrong password, unknown user, no accounts - answered alike after
    /// <see cref="RefusalDelay"/>. Either way the verdict carries the login note.
    /// </summary>
    /// <param name="login">The login as the client sent it.</param>
    /// <param name="cancellationToken">Cancels the check and the refusal delay.</param>
    /// <returns>Whether the login is accepted.</returns>
    public async ValueTask<SshLoginVerdict> CheckSshPasswordLoginAsync(
        SshPasswordLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        if (settings.AllowAnonymous)
        {
            return SshAcceptedUnchecked;
        }

        var isAccepted = settings.Accounts.CheckPassword(login.UserName, login.Password.Span);

        return await DecideSshLoginAsync(isAccepted, login.Method, login.UserName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// An SSH <c>publickey</c> request (ADR-0051, sections 6 and 7). Under <c>--allow-anonymous</c> a
    /// query is <see cref="SshLoginOutcome.KeyAcceptable"/> and a signed request
    /// <see cref="SshLoginOutcome.AcceptedUnchecked"/>. Otherwise a query is
    /// <see cref="SshLoginOutcome.KeyAcceptable"/> when the key is authorized for the user and
    /// <see cref="SshLoginOutcome.Refused"/> when not, undelayed and unnoted; a signed request is
    /// accepted only when the server verified its signature and the key is authorized, and refused
    /// after <see cref="RefusalDelay"/> otherwise, with the login note either way.
    /// </summary>
    /// <param name="login">The login as the client sent it, with the server's verdict on its signature.</param>
    /// <param name="cancellationToken">Cancels the check and the refusal delay.</param>
    /// <returns>Whether the key is acceptable or the login accepted.</returns>
    public async ValueTask<SshLoginVerdict> CheckSshPublicKeyLoginAsync(
        SshPublicKeyLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        var isQuery = login.Proof == SshPublicKeyProof.None;
        if (settings.AllowAnonymous)
        {
            return isQuery ? SshKeyAcceptable : SshAcceptedUnchecked;
        }

        var isAuthorized = settings.AuthorizedKeys.IsAuthorized(login.UserName, login.PublicKeyBlob.Span);
        if (isQuery)
        {
            return isAuthorized ? SshKeyAcceptable : SshRefusedUnchecked;
        }

        var isAccepted = isAuthorized & login.Proof == SshPublicKeyProof.ValidSignature;

        return await DecideSshLoginAsync(isAccepted, SshPublicKeyMethod, login.UserName, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SshLoginVerdict> DecideSshLoginAsync(
        bool isAccepted, string method, string? userName, CancellationToken cancellationToken)
    {
        if (isAccepted)
        {
            return new SshLoginVerdict(SshLoginOutcome.Accepted, userName, new CheckedLogin(method, userName, true));
        }

        await WaitRefusalDelayAsync(cancellationToken).ConfigureAwait(false);

        return new SshLoginVerdict(SshLoginOutcome.Refused, null, new CheckedLogin(method, userName, false));
    }

    /// <summary>
    /// Gets the accounts, the loosening options, the accepted methods, the authorized keys and the
    /// Kerberos acceptor this policy judges by, as <c>Surl.Console</c> composed them.
    /// </summary>
    public AuthenticationSettings Settings => settings;

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
