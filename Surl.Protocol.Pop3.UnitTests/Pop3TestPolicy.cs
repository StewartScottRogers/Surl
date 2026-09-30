using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// An authentication policy double for the POP3 tests: it accepts the one account <c>u</c> with
/// password <c>p</c>, or answers every login with <see cref="Verdict"/> when that is set; answers
/// the login with no credentials with <see cref="AnonymousVerdict"/>; offers a clear password,
/// SASL mechanisms and <c>APOP</c> as its properties say for each TLS state; and answers every
/// SASL exchange and <c>APOP</c> check with the next of <see cref="Steps"/>, in order. It records
/// what the server told it, so a test can assert the framing the server owns.
/// </summary>
internal sealed class Pop3TestPolicy : IAuthenticationPolicy, IMailAuthenticationPolicy
{
    private int nextStep;

    public PasswordLoginVerdict? Verdict { get; init; }

    public PasswordLoginVerdict AnonymousVerdict { get; init; } = PasswordLoginVerdict.RefusedAnonymous;

    public bool IsClearPasswordOffered { get; init; } = true;

    /// <summary>
    /// Whether a clear password is offered over TLS; <see langword="null"/> for <see cref="IsClearPasswordOffered"/>.
    /// </summary>
    public bool? IsClearPasswordOfferedOverTls { get; init; }

    public IReadOnlyList<string> SaslMechanisms { get; init; } = [];

    /// <summary>
    /// The mechanisms offered over TLS; <see langword="null"/> for <see cref="SaslMechanisms"/>.
    /// </summary>
    public IReadOnlyList<string>? SaslMechanismsOverTls { get; init; }

    public bool IsApopOffered { get; init; }

    /// <summary>
    /// Whether <c>APOP</c> is still offered once the connection is TLS.
    /// </summary>
    public bool IsApopOfferedOverTls { get; init; } = true;

    public IReadOnlyList<MailLoginStep> Steps { get; init; } = [];

    public List<PasswordLogin> Logins { get; } = [];

    public List<SaslExchangeStart> Starts { get; } = [];

    public List<byte[]> Responses { get; } = [];

    public List<ApopLogin> ApopLogins { get; } = [];

    public static MailLoginStep Challenge(byte[] challenge, CheckedLogin? checkedLogin = null) =>
        new(MailLoginOutcome.Challenge, challenge, null, checkedLogin);

    public static MailLoginStep Ended(MailLoginOutcome outcome, CheckedLogin? checkedLogin = null) =>
        new(outcome, ReadOnlyMemory<byte>.Empty, outcome == MailLoginOutcome.Accepted ? "u" : null, checkedLogin);

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        Logins.Add(login);
        var verdict = login.UserName is null ? AnonymousVerdict
            : Verdict ?? (login.UserName == "u" && login.Password is { } password && password.Span.SequenceEqual("p"u8)
                ? PasswordLoginVerdict.Accepted
                : PasswordLoginVerdict.RefusedCredentials);
        return ValueTask.FromResult(verdict);
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();

    public MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession) => tlsSession is null
        ? new(SaslMechanisms, IsClearPasswordOffered, IsApopOffered)
        : new(SaslMechanismsOverTls ?? SaslMechanisms, IsClearPasswordOfferedOverTls ?? IsClearPasswordOffered, IsApopOffered && IsApopOfferedOverTls);

    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        Starts.Add(start);
        return new ScriptedExchange(this);
    }

    public ValueTask<MailLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken)
    {
        ApopLogins.Add(login);
        return ValueTask.FromResult(NextStep());
    }

    private MailLoginStep NextStep() => Steps[nextStep++];

    private sealed class ScriptedExchange(Pop3TestPolicy policy) : ISaslExchange
    {
        public ValueTask<MailLoginStep> BeginAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(policy.NextStep());

        public ValueTask<MailLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
        {
            policy.Responses.Add(response.ToArray());
            return ValueTask.FromResult(policy.NextStep());
        }
    }
}
