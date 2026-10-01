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
    private readonly Queue<SaslLoginStep> steps;

    public ScriptedMailAuthenticationPolicy(IReadOnlyList<string> plaintextMechanisms, IReadOnlyList<string>? tlsMechanisms = null, params SaslLoginStep[] steps)
    {
        this.plaintextMechanisms = plaintextMechanisms;
        this.tlsMechanisms = tlsMechanisms ?? plaintextMechanisms;
        this.steps = new Queue<SaslLoginStep>(steps);
    }

    public List<TlsSession?> OffersAskedFor { get; } = [];

    public List<SaslExchangeStart> Starts { get; } = [];

    public List<byte[]> Responses { get; } = [];

    public static SaslLoginStep Challenge(byte[] challenge, CheckedLogin? checkedLogin = null) =>
        new(SaslLoginOutcome.Challenge, challenge, null, checkedLogin);

    public static SaslLoginStep Ended(SaslLoginOutcome outcome, CheckedLogin? checkedLogin = null) =>
        new(outcome, ReadOnlyMemory<byte>.Empty, outcome == SaslLoginOutcome.Accepted ? "user" : null, checkedLogin);

    public MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession)
    {
        OffersAskedFor.Add(tlsSession);
        return new MailLoginOffer(tlsSession is null ? plaintextMechanisms : tlsMechanisms, false, false);
    }

    public IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request) =>
        request.TlsSession is null ? plaintextMechanisms : tlsMechanisms;

    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        Starts.Add(start);
        return new ScriptedExchange(this);
    }

    public ValueTask<SaslLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException("SMTP has no APOP.");

    private sealed class ScriptedExchange(ScriptedMailAuthenticationPolicy policy) : ISaslExchange
    {
        public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(policy.steps.Dequeue());

        public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
        {
            policy.Responses.Add(response.ToArray());
            return ValueTask.FromResult(policy.steps.Dequeue());
        }
    }
}
