namespace Surl.Cli;

/// <summary>
/// One account given with <c>-u</c>/<c>--user</c> (ADR-0032 section 1): the value split at
/// its first <c>:</c>. An empty <see cref="UserName"/> makes <see cref="Password"/> a Bearer
/// token. <c>Surl.Console</c>'s <c>AuthenticationComposition</c> hands these to
/// <c>Surl.Authentication</c> as the accounts it checks.
/// </summary>
/// <param name="UserName">The user name, never holding a <c>:</c> or a control character; empty for a Bearer token.</param>
/// <param name="Password">The password (or token) in clear, never empty; may hold <c>:</c>.</param>
public sealed record CommandLineAccount(string UserName, string Password)
{
    /// <summary>
    /// Names the account without its password, so no log or exception text can hold one.
    /// </summary>
    /// <returns><c>CommandLineAccount { UserName = &lt;name&gt; }</c>.</returns>
    public override string ToString() => $"CommandLineAccount {{ UserName = {UserName} }}";
}
