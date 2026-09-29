using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// One HTTP connection's NTLM handshake over <c>Authorization: NTLM</c> (ADR-0039): the base64
/// message is decoded and answered by the connection's <see cref="NtlmHandshake"/>, and its
/// <c>CHALLENGE_MESSAGE</c> is sent back as <c>NTLM &lt;base64&gt;</c>.
/// </summary>
internal sealed class NtlmConnectionVerifier(AccountBook accounts, INtlmServerChallengeSource serverChallenges)
    : IHttpCredentialVerifier
{
    private readonly NtlmHandshake handshake = new(accounts, serverChallenges);

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

        var step = handshake.Answer(Base64Credentials.Decode(credentials));
        IReadOnlyList<string> values = step.ChallengeMessage is { } challengeMessage
            ? [$"NTLM {Convert.ToBase64String(challengeMessage)}"]
            : [];

        return ValueTask.FromResult(new HttpCredentialCheck(step.Outcome, step.AccountName, values, step.UserAsSent));
    }
}
