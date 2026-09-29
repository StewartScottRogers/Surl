namespace Surl.Authentication;

/// <summary>
/// One configured account, from a <c>--user</c> value or a <c>--user-file</c> line
/// (ADR-0032, sections 1 and 2). An empty <see cref="UserName"/> makes the
/// <see cref="Password"/> a Bearer token, never matched by a name-and-password method.
/// </summary>
/// <param name="UserName">The user name, never holding a <c>:</c> or a control character.</param>
/// <param name="Password">The password in clear, never empty.</param>
public sealed record Account(string UserName, string Password)
{
    /// <summary>
    /// Names the account without its password, so no log or exception text can hold one.
    /// </summary>
    /// <returns><c>Account { UserName = &lt;name&gt; }</c>.</returns>
    public override string ToString() => $"Account {{ UserName = {UserName} }}";
}
