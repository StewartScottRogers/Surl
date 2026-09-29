namespace Surl.Authentication;

/// <summary>
/// One connection's NTLM handshake over decoded messages (ADR-0039), whichever HTTP scheme
/// carries them: <c>NTLM</c> directly (<see cref="NtlmConnectionVerifier"/>) or inside
/// <c>Negotiate</c> (<see cref="NegotiateConnectionVerifier"/>, ADR-0040). A
/// <c>NEGOTIATE_MESSAGE</c> is answered with a <c>CHALLENGE_MESSAGE</c> over a new server
/// challenge, which the handshake keeps; any later message uses that challenge up, whatever the
/// verdict, so an <c>AUTHENTICATE_MESSAGE</c> is checked once and only against the challenge
/// this handshake issued. Only NTLMv2 answers are checked; anything else is refused, never thrown.
/// </summary>
internal sealed class NtlmHandshake(AccountBook accounts, INtlmServerChallengeSource serverChallenges)
{
    private static readonly NtlmHandshakeStep Refused = new(HttpCredentialOutcome.Refused, null, null, null);

    // An NTLMv1 response is exactly 24 bytes; NTLMv2's is NTProofStr and a longer blob.
    private const int NtlmV1ResponseLength = 24;

    private byte[]? serverChallenge;

    /// <summary>
    /// Answers one decoded message.
    /// </summary>
    /// <param name="message">The NTLM message, as the client sent it.</param>
    /// <returns>The <c>CHALLENGE_MESSAGE</c> to send, accepted as the account, or refused.</returns>
    public NtlmHandshakeStep Answer(byte[] message)
    {
        var challenge = serverChallenge;
        serverChallenge = null;

        return NtlmMessage.ReadMessageType(message) switch
        {
            NtlmMessage.NegotiateType => AnswerNegotiate(message),
            NtlmMessage.AuthenticateType => CheckAuthenticate(message, challenge),
            _ => Refused,
        };
    }

    /// <summary>
    /// Uses up any challenge issued, as a message the carrying scheme refused before it reached
    /// <see cref="Answer"/> must.
    /// </summary>
    public void Forget() => serverChallenge = null;

    private NtlmHandshakeStep AnswerNegotiate(byte[] message)
    {
        if (NtlmNegotiateMessage.ReadFlags(message) is not { } clientFlags)
        {
            return Refused;
        }

        serverChallenge = serverChallenges.CreateServerChallenge();

        return new NtlmHandshakeStep(
            HttpCredentialOutcome.Continue, null, NtlmChallengeMessage.Create(clientFlags, serverChallenge), null);
    }

    private NtlmHandshakeStep CheckAuthenticate(byte[] message, byte[]? challenge)
    {
        var answer = NtlmAuthenticateMessage.TryRead(message);
        if (answer is null)
        {
            return Refused;
        }

        var accountName = challenge is not null && answer.NtChallengeResponse.Length > NtlmV1ResponseLength
            ? FindAnsweringAccount(answer, challenge)
            : null;

        return accountName is null
            ? Refused with { UserAsSent = answer.UserName }
            : new NtlmHandshakeStep(HttpCredentialOutcome.Accepted, accountName, null, answer.UserName);
    }

    private string? FindAnsweringAccount(NtlmAuthenticateMessage answer, byte[] challenge)
    {
        var account = accounts.FindNtlmAccount(answer.UserName);
        var response = answer.NtChallengeResponse.AsSpan();
        var responseKey = NtlmV2Calculation.ComputeResponseKeyNt(account.NtHash, answer.UserName, answer.DomainName);
        var expectedProof = NtlmV2Calculation.ComputeNtProof(
            responseKey, challenge, response[NtlmV2Calculation.NtProofLength..]);
        var matches = accounts.SecretComparer.FixedTimeEquals(
            expectedProof, response[..NtlmV2Calculation.NtProofLength]);

        return matches ? account.AccountName : null;
    }
}
