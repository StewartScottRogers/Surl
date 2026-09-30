using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// A scripted <see cref="IMailAuthenticationPolicy"/>: it offers the mechanisms it is given for
/// each TLS state, and every exchange it starts answers with the next of its steps, in order. It
/// records what the server told it, so a test can assert the framing the server owns.
/// </summary>
internal sealed class ScriptedMailAuthenticationPolicy : IMailAuthenticationPolicy
{
    private readonly IReadOnlyList<string> plaintextMechanisms;
    private readonly IReadOnlyList<string> tlsMechanisms;
    private readonly Queue<MailLoginStep> steps;

    public ScriptedMailAuthenticationPolicy(IReadOnlyList<string> plaintextMechanisms, IReadOnlyList<string>? tlsMechanisms = null, params MailLoginStep[] steps)
    {
        this.plaintextMechanisms = plaintextMechanisms;
        this.tlsMechanisms = tlsMechanisms ?? plaintextMechanisms;
        this.steps = new Queue<MailLoginStep>(steps);
    }

    public List<TlsSession?> OffersAskedFor { get; } = [];

    public List<SaslExchangeStart> Starts { get; } = [];

    public List<byte[]> Responses { get; } = [];

    public static MailLoginStep Challenge(byte[] challenge, CheckedLogin? checkedLogin = null) =>
        new(MailLoginOutcome.Challenge, challenge, null, checkedLogin);

    public static MailLoginStep Ended(MailLoginOutcome outcome, CheckedLogin? checkedLogin = null) =>
        new(outcome, ReadOnlyMemory<byte>.Empty, outcome == MailLoginOutcome.Accepted ? "user" : null, checkedLogin);

    public MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession)
    {
        OffersAskedFor.Add(tlsSession);
        return new MailLoginOffer(tlsSession is null ? plaintextMechanisms : tlsMechanisms, false, false);
    }

    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        Starts.Add(start);
        return new ScriptedExchange(this);
    }

    public ValueTask<MailLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException("SMTP has no APOP.");

    private sealed class ScriptedExchange(ScriptedMailAuthenticationPolicy policy) : ISaslExchange
    {
        public ValueTask<MailLoginStep> BeginAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(policy.steps.Dequeue());

        public ValueTask<MailLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
        {
            policy.Responses.Add(response.ToArray());
            return ValueTask.FromResult(policy.steps.Dequeue());
        }
    }
}
