using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>NTLM</c> (ADR-0049, section 5): an empty challenge when no initial response was sent,
/// then the client's <c>NEGOTIATE_MESSAGE</c> answered with ADR-0039's <c>CHALLENGE_MESSAGE</c>
/// and its NTLMv2 <c>AUTHENTICATE_MESSAGE</c> checked, both by the <see cref="NtlmHandshake"/>
/// HTTP NTLM uses, which this exchange holds, so it dies with the <c>AUTH</c> command. One
/// <c>CHALLENGE_MESSAGE</c> is issued per exchange: a second <c>NEGOTIATE_MESSAGE</c>, a
/// malformed message, a wrong answer or an <c>AUTHENTICATE_MESSAGE</c> before any challenge is
/// refused as a bad credential after the delay, naming the user the message carried, if any
/// (ADR-0038). Under <c>--allow-anonymous</c> every step still runs and the message after the
/// challenge is accepted unchecked.
/// </summary>
internal sealed class NtlmSaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    private readonly NtlmHandshake handshake = context.StartNtlmHandshake();
    private bool isChallengeIssued;

    /// <inheritdoc/>
    protected override ValueTask<SaslLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (response is not { } message)
        {
            return ValueTask.FromResult(Challenge(ReadOnlyMemory<byte>.Empty));
        }

        var step = handshake.Answer(message.ToArray());
        if (step.ChallengeMessage is { } challengeMessage && !isChallengeIssued)
        {
            isChallengeIssued = true;
            return ValueTask.FromResult(Challenge(challengeMessage));
        }

        return Conclude(step, cancellationToken);
    }

    // Ends the exchange on anything but its one CHALLENGE_MESSAGE.
    private ValueTask<SaslLoginStep> Conclude(NtlmHandshakeStep step, CancellationToken cancellationToken)
    {
        if (Context.IsUnchecked)
        {
            return ValueTask.FromResult(AcceptedUnchecked);
        }

        return step.AccountName is { } accountName
            ? ValueTask.FromResult(Accept(accountName, step.UserAsSent))
            : RefuseAsync(step.UserAsSent, cancellationToken);
    }
}
