namespace Surl.Api;

/// <summary>
/// An account a client may log in as (<c>--user name:password</c>, ADR-0032 section 1), held in
/// <see cref="SurlServerOptions.Accounts"/>.
/// </summary>
/// <param name="UserName">The user name; not null, and without a <c>:</c>, since <c>--user</c> splits at the first one.</param>
/// <param name="Password">The password; not null, and not empty, as <c>--user</c> requires.</param>
public sealed record SurlAccount(string UserName, string Password)
{
    /// <summary>Names the account without its password, so no log or exception text can hold one.</summary>
    /// <returns><c>SurlAccount { UserName = &lt;name&gt; }</c>.</returns>
    public override string ToString() => $"SurlAccount {{ UserName = {UserName} }}";
}
