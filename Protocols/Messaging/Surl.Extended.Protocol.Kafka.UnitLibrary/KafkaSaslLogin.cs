using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// One SASL login after a SaslHandshake (ADR-0105, decision 5): each client message - a
/// SaslAuthenticate's <c>auth_bytes</c> after a v1 handshake, or a raw token after a v0 one - goes
/// to the policy's <see cref="ISaslExchange"/>, and its step becomes the server's message. The
/// policy owns the mechanism; this class owns the Kafka framing of it.
/// </summary>
/// <remarks>
/// SCRAM's server-final message (RFC 5802) is the mechanism's additional data with success
/// (RFC 4422, section 5), which Kafka carries in the response that ends the login: a Kafka SCRAM
/// client sends nothing after it. The policy issues it as a second challenge, as the mail servers
/// need, so the login answers that challenge itself with the empty response a mail client would
/// send, and returns the server-final with the outcome.
/// </remarks>
/// <param name="policy">The SASL policy.</param>
/// <param name="mechanism">The mechanism the handshake chose.</param>
/// <param name="handshakeVersion">The SaslHandshake version: 0 carries raw tokens, 1 SaslAuthenticate requests.</param>
internal sealed class KafkaSaslLogin(ISaslAuthenticationPolicy policy, string mechanism, short handshakeVersion)
{
    private const int ScramServerFinalChallenge = 2;

    private ISaslExchange? exchange;
    private int challengesIssued;

    /// <summary>The mechanism the handshake chose.</summary>
    public string Mechanism { get; } = mechanism;

    /// <summary>Whether the client messages come as raw tokens (after a v0 handshake).</summary>
    public bool CarriesRawTokens { get; } = handshakeVersion == 0;

    /// <summary>Hands the policy one client message and returns the server's step.</summary>
    /// <param name="clientMessage">The client's message.</param>
    /// <param name="state">The connection, whose login ends here when the step does.</param>
    /// <param name="context">The exchange's context.</param>
    /// <returns>A challenge to send, or how the login ended.</returns>
    public async ValueTask<KafkaSaslStep> StepAsync(ReadOnlyMemory<byte> clientMessage, KafkaConnectionState state, ExchangeContext context)
    {
        var step = await NextStepAsync(clientMessage, state, context);
        if (step.Outcome != SaslLoginOutcome.Challenge)
        {
            return End(step, step.AdditionalSuccessData, state);
        }

        challengesIssued++;
        if (!IsScramServerFinal())
        {
            return new KafkaSaslStep(KafkaSaslStepOutcome.Challenge, step.Challenge);
        }

        var final = await exchange!.ContinueAsync(ReadOnlyMemory<byte>.Empty, context.CancellationToken);
        Note(final, context);
        return End(final, step.Challenge, state);
    }

    private async ValueTask<SaslLoginStep> NextStepAsync(ReadOnlyMemory<byte> clientMessage, KafkaConnectionState state, ExchangeContext context)
    {
        SaslLoginStep step;
        if (exchange is null)
        {
            exchange = policy.StartSaslExchange(new SaslExchangeStart(context.Scheme, Mechanism, clientMessage, state.TlsSession));
            step = await exchange.BeginAsync(context.CancellationToken);
        }
        else
        {
            step = await exchange.ContinueAsync(clientMessage, context.CancellationToken);
        }

        Note(step, context);
        return step;
    }

    private bool IsScramServerFinal() =>
        Mechanism.StartsWith("SCRAM-", StringComparison.Ordinal) && challengesIssued == ScramServerFinalChallenge;

    // Accepted, checked or not, ends the login with its account; anything else refuses it.
    private static KafkaSaslStep End(SaslLoginStep step, ReadOnlyMemory<byte> serverMessage, KafkaConnectionState state)
    {
        var isAccepted = step.Outcome is SaslLoginOutcome.Accepted or SaslLoginOutcome.AcceptedUnchecked;
        state.EndLogin(isAccepted, isAccepted ? step.AccountName : null);
        return isAccepted
            ? new KafkaSaslStep(KafkaSaslStepOutcome.Accepted, serverMessage)
            : new KafkaSaslStep(KafkaSaslStepOutcome.Refused, ReadOnlyMemory<byte>.Empty);
    }

    private static void Note(SaslLoginStep step, ExchangeContext context)
    {
        if (step.CheckedLogin is { } checkedLogin)
        {
            context.Log.Note(checkedLogin.Note);
        }

        if (step.RefusalNote is { } refusalNote)
        {
            context.Log.Note(refusalNote);
        }
    }
}
