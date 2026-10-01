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
/// <para>
/// There, too, a first token that is SPNEGO (RFC 4178) runs the same handshake by ADR-0040
/// decision 3: a <c>NegTokenInit</c> naming NTLM, then <c>negTokenResp</c>s both ways, the
/// challenge in an <c>accept-incomplete</c> one and the success carrying an
/// <c>accept-completed</c> one as <see cref="SaslLoginStep.AdditionalSuccessData"/>. A client
/// <c>mechListMIC</c> is checked against the login's keys and answered with the server's; one
/// that does not match, or none where NTLM was not the client's first mechanism, is refused
/// (ADR-0072, decision 4). <c>WinLDAP</c> sends bare NTLM, measured (BL-330).
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

    /// <summary>
    /// The refusal note for an SPNEGO login whose <c>mechListMIC</c> is not the one the
    /// login's keys sign.
    /// </summary>
    public const string WrongMechListMicNote = "the SPNEGO mechListMIC does not match";

    /// <summary>
    /// The refusal note for an SPNEGO login that sent no <c>mechListMIC</c> although NTLM was not
    /// its first mechanism (RFC 4178 section 5).
    /// </summary>
    public const string MissingMechListMicNote = "SPNEGO needs a mechListMIC when NTLM is not the first mechanism";

    private readonly NtlmHandshake handshake = context.StartNtlmHandshake();
    private bool isChallengeIssued;

    // The DER of the client's MechTypeList once its first token was SPNEGO; null for bare NTLM.
    private byte[]? spnegoMechTypes;

    // Whether NTLM was not the client's first mechanism, so its mechListMIC must come.
    private bool isMechListMicRequired;

    // Whether a negTokenResp has named NTLM as supportedMech, which only the first one does.
    private bool isSupportedMechSent;

    /// <inheritdoc/>
    protected override ValueTask<SaslLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (response is not { } message)
        {
            return ValueTask.FromResult(Challenge(ReadOnlyMemory<byte>.Empty));
        }

        var token = message.ToArray();
        if (spnegoMechTypes is not null)
        {
            return AnswerNegTokenResp(token, cancellationToken);
        }

        return IsFirstSpnegoToken(token)
            ? AnswerNegTokenInit(token, cancellationToken)
            : AnswerBareNtlm(token, cancellationToken);
    }

    // Only a bind's first message may open SPNEGO; mail NTLM is always bare.
    private bool IsFirstSpnegoToken(byte[] token) =>
        Context.CanCarrySecurityLayer && !isChallengeIssued && SpnegoToken.IsInitialContextToken(token);

    private ValueTask<SaslLoginStep> AnswerBareNtlm(byte[] token, CancellationToken cancellationToken)
    {
        var step = handshake.Answer(token);
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

    // ADR-0040 decision 3 in a bind: NTLM is picked from the client's list. Its optimistic token
    // is NTLM's first message only when NTLM is the client's first choice; otherwise the reply
    // only names NTLM and the client starts it in its next token.
    private ValueTask<SaslLoginStep> AnswerNegTokenInit(byte[] token, CancellationToken cancellationToken)
    {
        var negTokenInit = SpnegoToken.ReadNegTokenInit(token);
        if (negTokenInit is null || !negTokenInit.MechTypes.Contains(SpnegoToken.NtlmOid))
        {
            return RefuseUnreadableAsync(cancellationToken);
        }

        spnegoMechTypes = negTokenInit.MechTypesDer;
        isMechListMicRequired = negTokenInit.MechTypes[0] != SpnegoToken.NtlmOid;
        if (isMechListMicRequired || negTokenInit.MechToken is null)
        {
            isSupportedMechSent = true;
            return ValueTask.FromResult(
                Challenge(SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptIncomplete, SpnegoToken.NtlmOid, null)));
        }

        return AnswerSpnegoNtlm(negTokenInit.MechToken, null, cancellationToken);
    }

    private ValueTask<SaslLoginStep> AnswerNegTokenResp(byte[] token, CancellationToken cancellationToken) =>
        SpnegoToken.ReadNegTokenResp(token) is { } negTokenResp
            ? AnswerSpnegoNtlm(negTokenResp.ResponseToken, negTokenResp.MechListMic, cancellationToken)
            : RefuseUnreadableAsync(cancellationToken);

    // The bare exchange's steps, each NTLM message carried in a negTokenResp both ways.
    private ValueTask<SaslLoginStep> AnswerSpnegoNtlm(byte[] ntlmMessage, byte[]? mechListMic, CancellationToken cancellationToken)
    {
        var step = handshake.Answer(ntlmMessage);
        if (step.ChallengeMessage is { } challengeMessage && !isChallengeIssued)
        {
            isChallengeIssued = true;
            var supportedMech = isSupportedMechSent ? null : SpnegoToken.NtlmOid;
            return ValueTask.FromResult(
                Challenge(SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptIncomplete, supportedMech, challengeMessage)));
        }

        return ConcludeSpnegoAsync(step, mechListMic, cancellationToken);
    }

    // As ConcludeWithSecurityLayer, then the client's mechListMIC checked and the server's sent in
    // the accept-completed negTokenResp that goes with success. Both are NTLM signatures with
    // sequence number 0, so the first protected message in each direction carries 1 (ADR-0072,
    // decision 4; [MS-SPNG] section 3.1.5.1).
    private async ValueTask<SaslLoginStep> ConcludeSpnegoAsync(
        NtlmHandshakeStep step, byte[]? mechListMic, CancellationToken cancellationToken)
    {
        var concluded = await ConcludeWithSecurityLayer(step, cancellationToken);
        if (concluded.Outcome != SaslLoginOutcome.Accepted)
        {
            return concluded;
        }

        if (mechListMic is not null)
        {
            return await ConcludeWithMechListMicAsync(step, concluded, mechListMic, cancellationToken);
        }

        return isMechListMicRequired
            ? await RefuseAsync(step.UserAsSent, cancellationToken, MissingMechListMicNote)
            : concluded with { AdditionalSuccessData = SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptCompleted, null, null) };
    }

    private async ValueTask<SaslLoginStep> ConcludeWithMechListMicAsync(
        NtlmHandshakeStep step, SaslLoginStep concluded, byte[] mechListMic, CancellationToken cancellationToken)
    {
        // An accepted login exports its session key; the mechListMIC needs extended session security.
        var sessionKey = step.SessionKey!;
        if (!sessionKey.NegotiateFlags.HasFlag(NtlmNegotiateFlags.ExtendedSessionSecurity))
        {
            return await RefuseAsync(step.UserAsSent, cancellationToken, NoExtendedSessionSecurityNote);
        }

        var layer = concluded.SecurityLayer as NtlmSecurityLayer ?? NtlmSecurityLayer.ForAcceptor(sessionKey);
        if (!layer.VerifyMechListMic(spnegoMechTypes!, mechListMic))
        {
            return await RefuseAsync(step.UserAsSent, cancellationToken, WrongMechListMicNote);
        }

        var serverMechListMic = layer.SignMechListMic(spnegoMechTypes!);
        return concluded with
        {
            AdditionalSuccessData = SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptCompleted, null, null, serverMechListMic),
        };
    }

    // A token that is not SPNEGO naming NTLM, refused as an unreadable NTLM message is.
    private ValueTask<SaslLoginStep> RefuseUnreadableAsync(CancellationToken cancellationToken) =>
        RefuseAsync(null, cancellationToken, Context.IsUnchecked ? SecurityLayerNeedsPasswordNote : null);

    private bool HasAccount(string? user) =>
        user is not null && Context.Accounts.FindNtlmAccount(user).AccountName is not null;
}
