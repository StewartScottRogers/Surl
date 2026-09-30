namespace Surl.Protocol.Abstractions;

/// <summary>
/// How an <see cref="ISshAuthenticationPolicy"/> judged an SSH login (ADR-0051, section 7). The
/// server acts on it alone.
/// </summary>
/// <param name="Outcome">Whether the login is accepted, the key acceptable, or the login refused.</param>
/// <param name="AccountName">
/// The account logged in when <see cref="SshLoginOutcome.Accepted"/>; otherwise <see langword="null"/>.
/// </param>
/// <param name="CheckedLogin">
/// The credential checked and the answer, which the server writes to the verbose log as
/// <see cref="CheckedLogin.Note"/> before it answers; <see langword="null"/> when nothing was checked.
/// </param>
public sealed record SshLoginVerdict(SshLoginOutcome Outcome, string? AccountName, CheckedLogin? CheckedLogin);
