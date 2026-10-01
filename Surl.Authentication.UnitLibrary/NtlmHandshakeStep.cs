namespace Surl.Authentication;

/// <summary>
/// How an <see cref="NtlmHandshake"/> answered one message.
/// </summary>
/// <param name="Outcome">Accepted, refused, or <c>Continue</c> with a challenge to send.</param>
/// <param name="AccountName">The account logged in when accepted; otherwise <see langword="null"/>.</param>
/// <param name="ChallengeMessage">The <c>CHALLENGE_MESSAGE</c> to send on <c>Continue</c>; otherwise <see langword="null"/>.</param>
/// <param name="UserAsSent">The <c>AUTHENTICATE_MESSAGE</c>'s user name, when one was read.</param>
/// <param name="SessionKey">
/// The exported session key and the client's flags when accepted by a handshake that grants a
/// security layer; otherwise <see langword="null"/>.
/// </param>
internal sealed record NtlmHandshakeStep(
    HttpCredentialOutcome Outcome,
    string? AccountName,
    byte[]? ChallengeMessage,
    string? UserAsSent,
    NtlmSessionKey? SessionKey = null);
