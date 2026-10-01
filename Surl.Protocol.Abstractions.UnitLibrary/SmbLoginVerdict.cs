namespace Surl.Protocol.Abstractions;

/// <summary>
/// How an <see cref="ISmbAuthenticationPolicy"/> judged an SMB session setup (ADR-0073,
/// decision 3). The server acts on it alone.
/// </summary>
/// <param name="Outcome">Whether the login is accepted, accepted unchecked, or refused.</param>
/// <param name="AccountName">
/// The account logged in when <see cref="SmbLoginOutcome.Accepted"/>; otherwise <see langword="null"/>.
/// </param>
/// <param name="CheckedLogin">
/// The credential checked and the answer, which the server writes to the verbose log as
/// <see cref="CheckedLogin.Note"/> before it answers; <see langword="null"/> when nothing was
/// checked, which with <see cref="SmbLoginOutcome.Refused"/> means <c>ntlmv1</c> is not in
/// <c>--auth</c>.
/// </param>
public sealed record SmbLoginVerdict(SmbLoginOutcome Outcome, string? AccountName, CheckedLogin? CheckedLogin);
