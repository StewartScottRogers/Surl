using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// A login policy whose answers a test sets: the verdict for a login with a password, the
/// verdict for the login that carries none, whether the clear-password login is offered and which
/// SASL mechanisms are, each for a plaintext and a TLS connection, and the steps every SASL
/// exchange answers with, in order. It records every password login it is asked about and what
/// the server handed each exchange, so a test can assert the framing the server owns.
/// </summary>
internal sealed class ScriptedLoginPolicy(
    PasswordLoginVerdict passwordVerdict = PasswordLoginVerdict.Accepted,
    PasswordLoginVerdict anonymousVerdict = PasswordLoginVerdict.RefusedAnonymous,
    bool isClearPasswordLoginOffered = true) : IAuthenticationPolicy, IMailAuthenticationPolicy
{
    private int nextStep;

    /// <summary>
    /// Whether the clear-password login is offered over TLS; <see langword="null"/> for the
    /// plaintext answer.
    /// </summary>
    public bool? IsClearPasswordLoginOfferedOverTls { get; init; }

    public IReadOnlyList<string> SaslMechanisms { get; init; } = [];

    /// <summary>
    /// The mechanisms offered over TLS; <see langword="null"/> for <see cref="SaslMechanisms"/>.
    /// </summary>
    public IReadOnlyList<string>? SaslMechanismsOverTls { get; init; }

    public IReadOnlyList<SaslLoginStep> Steps { get; init; } = [];

    public List<PasswordLogin> Logins { get; } = [];

    public List<SaslExchangeStart> Starts { get; } = [];

    public List<byte[]> Responses { get; } = [];

    public static SaslLoginStep Challenge(byte[] challenge, CheckedLogin? checkedLogin = null) =>
        new(SaslLoginOutcome.Challenge, challenge, null, checkedLogin);

    public static SaslLoginStep Ended(SaslLoginOutcome outcome, CheckedLogin? checkedLogin = null) =>
        new(outcome, ReadOnlyMemory<byte>.Empty, outcome == SaslLoginOutcome.Accepted ? "u" : null, checkedLogin);

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        Logins.Add(login);
        return ValueTask.FromResult(login.Password is null ? anonymousVerdict : passwordVerdict);
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();

    public MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession) => tlsSession is null
        ? new(SaslMechanisms, isClearPasswordLoginOffered, false)
        : new(SaslMechanismsOverTls ?? SaslMechanisms, IsClearPasswordLoginOfferedOverTls ?? isClearPasswordLoginOffered, false);

    public IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request) =>
        GetMailLoginOffer(request.TlsSession).SaslMechanisms;

    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        Starts.Add(start);
        return new ScriptedExchange(this);
    }

    public ValueTask<SaslLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken) => throw new NotSupportedException();

    private SaslLoginStep NextStep() => Steps[nextStep++];

    private sealed class ScriptedExchange(ScriptedLoginPolicy policy) : ISaslExchange
    {
        public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(policy.NextStep());

        public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
        {
            policy.Responses.Add(response.ToArray());
            return ValueTask.FromResult(policy.NextStep());
        }
    }
}
