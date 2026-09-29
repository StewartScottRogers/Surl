namespace Surl.Authentication;

/// <summary>
/// How an <see cref="IHttpCredentialVerifier"/> judged one <c>Authorization</c> field.
/// </summary>
/// <param name="Outcome">Accepted, refused, or a handshake's next step.</param>
/// <param name="AccountName">The account logged in when accepted; otherwise <see langword="null"/>.</param>
/// <param name="WwwAuthenticateValues">
/// The continuation step's one <c>WWW-Authenticate</c> value, or the values to send on an
/// accepted response (Negotiate's final token); empty otherwise.
/// </param>
/// <param name="UserAsSent">
/// The user name the credentials carried, as sent and whatever the outcome, for the verbose
/// log's login note (ADR-0032, section 8): <c>bearer token</c> for Bearer, and
/// <see langword="null"/> when none could be read. Never a password or token.
/// </param>
/// <param name="CheckBody">
/// For <see cref="HttpCredentialOutcome.AwaitingBody"/>, the rest of the check: given the body's
/// SHA-256, it answers <see cref="HttpCredentialOutcome.Accepted"/> or
/// <see cref="HttpCredentialOutcome.Refused"/> (ADR-0045); otherwise <see langword="null"/>.
/// </param>
public sealed record HttpCredentialCheck(
    HttpCredentialOutcome Outcome,
    string? AccountName,
    IReadOnlyList<string> WwwAuthenticateValues,
    string? UserAsSent = null,
    Func<ReadOnlyMemory<byte>, HttpCredentialCheck>? CheckBody = null);
