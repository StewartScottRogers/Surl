using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>NTLM</c> (ADR-0049, section 5), and LDAP's <c>NTLM</c> and <c>GSS-SPNEGO</c> binds
/// carrying a bare NTLM message (ADR-0072, decision 4): an empty challenge when no initial
/// response was sent, then the client's <c>NEGOTIATE_MESSAGE</c> answered with a
/// <c>CHALLENGE_MESSAGE</c> and its NTLMv2 <c>AUTHENTICATE_MESSAGE</c> checked, both by the
/// <see cref="NtlmHandshake"/> HTTP NTLM uses, which this exchange holds, so it dies with the
/// login. One <c>CHALLENGE_MESSAGE</c> is issued per exchange: a second
/// <c>NEGOTIATE_MESSAGE</c>, a malformed message, a wrong answer or an
/// <c>AUTHENTICATE_MESSAGE</c> before any challenge is refused as a bad credential after the
/// delay, naming the user the message carried, if any (ADR-0038).
/// <para>
/// For the mail servers the challenge is ADR-0039's, and under <c>--allow-anonymous</c> every
/// step still runs and the message after the challenge is accepted unchecked. Where the server
/// carries a security layer (<see cref="SaslExchangeContext.CanCarrySecurityLayer"/>, LDAP) the
/// challenge also grants signing, sealing and key exchange as asked, the login is always
/// checked - under <c>--allow-anonymous</c> a user with no account is refused with a note, since
/// the layer's keys come from the account's password - and an accepted login whose
/// <c>AUTHENTICATE_MESSAGE</c> asks for sealing or signing carries an
/// <see cref="NtlmSecurityLayer"/>; one asking for either without extended session security
/// is refused.
/// </para>
/// </summary>
internal sealed class NtlmSaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    /// <summary>
    /// The refusal note for a user with no account under <c>--allow-anonymous</c> where a security
    /// layer follows (ADR-0072, decision 4).
    /// </summary>
    public const string SecurityLayerNeedsPasswordNote = "the security layer needs the account's password";

    /// <summary>
    /// The refusal note for a login asking for sealing or signing without extended session
    /// security, the only form Surl's <see cref="NtlmSecurityLayer"/> runs.
    /// </summary>
    public const string NoExtendedSessionSecurityNote = "NTLM signing and sealing need extended session security";

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

        return Context.CanCarrySecurityLayer
            ? ConcludeWithSecurityLayer(step, cancellationToken)
            : Conclude(step, cancellationToken);
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

    // As Conclude, where a security layer may follow: never unchecked (ADR-0072, decision 4).
    private ValueTask<SaslLoginStep> ConcludeWithSecurityLayer(NtlmHandshakeStep step, CancellationToken cancellationToken)
    {
        if (step.AccountName is not { } accountName)
        {
            var note = Context.IsUnchecked && !HasAccount(step.UserAsSent) ? SecurityLayerNeedsPasswordNote : null;
            return RefuseAsync(step.UserAsSent, cancellationToken, note);
        }

        // A handshake that grants a security layer exports a session key with every acceptance.
        var sessionKey = step.SessionKey!;
        if (!NtlmSecurityLayer.IsNegotiated(sessionKey.NegotiateFlags))
        {
            return ValueTask.FromResult(Accept(accountName, step.UserAsSent));
        }

        if (!sessionKey.NegotiateFlags.HasFlag(NtlmNegotiateFlags.ExtendedSessionSecurity))
        {
            return RefuseAsync(step.UserAsSent, cancellationToken, NoExtendedSessionSecurityNote);
        }

        return ValueTask.FromResult(
            Accept(accountName, step.UserAsSent) with { SecurityLayer = NtlmSecurityLayer.ForAcceptor(sessionKey) });
    }

    private bool HasAccount(string? user) =>
        user is not null && Context.Accounts.FindNtlmAccount(user).AccountName is not null;
}
