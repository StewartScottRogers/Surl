namespace Surl.Protocol.Abstractions;

/// <summary>
/// A login whose credentials were checked, and the check's answer, as a protocol server writes
/// it to the verbose log (ADR-0032, sections 6 and 8). It holds no password, token or
/// <c>Authorization</c> value, so its <see cref="Note"/> never can.
/// </summary>
/// <param name="Method">
/// The method checked: the HTTP <c>Authorization</c> scheme (<c>Basic</c>, <c>Digest</c>,
/// <c>Bearer</c>, ...), or the listen URL's scheme for a password login (<c>mqtt</c>).
/// </param>
/// <param name="User">
/// The user name as sent, <c>bearer token</c> for Bearer, or <see langword="null"/> when no user
/// name could be read from the credentials.
/// </param>
/// <param name="IsAccepted">Whether the credentials matched an account.</param>
public sealed record CheckedLogin(string Method, string? User, bool IsAccepted)
{
    /// <summary>
    /// What Bearer's <see cref="User"/> says in place of the token (ADR-0032, section 8).
    /// </summary>
    public const string BearerTokenUser = "bearer token";

    /// <summary>
    /// The verbose-log note: <c>Login accepted: &lt;method&gt; &lt;user&gt;</c> or
    /// <c>Login refused: &lt;method&gt; &lt;user&gt;</c>, with <c> &lt;user&gt;</c> left out when
    /// <see cref="User"/> is <see langword="null"/>. The log escapes it (ADR-0007, section 8).
    /// </summary>
    public string Note =>
        $"Login {(IsAccepted ? "accepted" : "refused")}: {Method}{(User is null ? string.Empty : " " + User)}";
}
