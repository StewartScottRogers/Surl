using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// One HTTP connection's NTLM handshake (ADR-0039). A <c>NEGOTIATE_MESSAGE</c> is answered with
/// a <c>Continue</c> carrying a <c>CHALLENGE_MESSAGE</c> over a new server challenge, which the
/// connection keeps; an <c>AUTHENTICATE_MESSAGE</c> is checked against that challenge and uses
/// it up, whatever the verdict, so an answer is good once and only on the connection that was
/// challenged. Only NTLMv2 answers are checked; anything else is refused, never thrown.
/// </summary>
internal sealed class NtlmConnectionVerifier(AccountBook accounts, INtlmServerChallengeSource serverChallenges)
    : IHttpCredentialVerifier
{
    private static readonly HttpCredentialCheck Refused = new(HttpCredentialOutcome.Refused, null, []);

    // An NTLMv1 response is exactly 24 bytes; NTLMv2's is NTProofStr and a longer blob.
    private const int NtlmV1ResponseLength = 24;

    private byte[]? serverChallenge;

    /// <summary>
    /// Answers one leg of the handshake.
    /// </summary>
    /// <param name="credentials">The base64 message after <c>NTLM</c>.</param>
    /// <param name="request">The request the field arrived on.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The challenge, accepted as the account, or refused.</returns>
    public ValueTask<HttpCredentialCheck> VerifyAsync(
        string credentials, HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var challenge = serverChallenge;
        serverChallenge = null;
        var message = DecodeBase64(credentials);

        return ValueTask.FromResult(NtlmMessage.ReadMessageType(message) switch
        {
            NtlmMessage.NegotiateType => AnswerNegotiate(message),
            NtlmMessage.AuthenticateType => CheckAuthenticate(message, challenge),
            _ => Refused,
        });
    }

    private static byte[] DecodeBase64(string credentials)
    {
        var buffer = new byte[credentials.Length];

        return Convert.TryFromBase64String(credentials, buffer, out var written) ? buffer[..written] : [];
    }

    private HttpCredentialCheck AnswerNegotiate(byte[] message)
    {
        if (NtlmNegotiateMessage.ReadFlags(message) is not { } clientFlags)
        {
            return Refused;
        }

        serverChallenge = serverChallenges.CreateServerChallenge();
        var challengeMessage = NtlmChallengeMessage.Create(clientFlags, serverChallenge);

        return new HttpCredentialCheck(
            HttpCredentialOutcome.Continue, null, [$"NTLM {Convert.ToBase64String(challengeMessage)}"]);
    }

    private HttpCredentialCheck CheckAuthenticate(byte[] message, byte[]? challenge)
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
            : new HttpCredentialCheck(HttpCredentialOutcome.Accepted, accountName, [], answer.UserName);
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
