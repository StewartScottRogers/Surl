namespace Surl.Authentication;

/// <summary>
/// One account as <see cref="AccountBook"/> keeps it: its name, the schemes it may log in on, the
/// SHA-256 of its password and, for a named account, every credential derived from the password
/// once, when the account was read at start or added (ADR-0032, section 8; ADR-0124, decision 3).
/// The clear password is not kept.
/// </summary>
/// <param name="UserName">The user name; empty for a Bearer token's account.</param>
/// <param name="Schemes">The schemes it may log in on, as <see cref="Account.Schemes"/>.</param>
/// <param name="PasswordHash">The SHA-256 of the password's UTF-8 bytes.</param>
/// <param name="Named">The derived credentials, or <see langword="null"/> for the empty name.</param>
internal sealed record DerivedAccount(
    string UserName,
    IReadOnlySet<string>? Schemes,
    byte[] PasswordHash,
    NamedAccountCredentials? Named)
{
    /// <summary>
    /// Whether a login on <paramref name="scheme"/> may reach this account, as
    /// <see cref="Account.MayLogInOn"/> decides.
    /// </summary>
    /// <param name="scheme">The scheme, compared without regard to case.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    public bool MayLogInOn(string scheme) => Account.SchemesAdmit(Schemes, scheme);
}
