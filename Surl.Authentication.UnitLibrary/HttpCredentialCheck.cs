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
public sealed record HttpCredentialCheck(
    HttpCredentialOutcome Outcome,
    string? AccountName,
    IReadOnlyList<string> WwwAuthenticateValues);
