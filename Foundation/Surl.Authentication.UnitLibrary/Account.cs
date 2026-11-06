namespace Surl.Authentication;

/// <summary>
/// One configured account, from a <c>--user</c> value, a <c>--user-file</c> line or a
/// <c>--mailbox</c> (ADR-0032, sections 1 and 2; ADR-0102, decision 1.2). An empty
/// <see cref="UserName"/> makes the <see cref="Password"/> a Bearer token, never matched by a
/// name-and-password method.
/// </summary>
/// <param name="UserName">The user name, never holding a <c>:</c> or a control character.</param>
/// <param name="Password">The password in clear, never empty.</param>
public sealed record Account(string UserName, string Password)
{
    /// <summary>
    /// The schemes a <c>--mailbox</c> account logs in on when its <c>protocols=</c> is absent:
    /// the mail schemes, each admitting its TLS twin (ADR-0102, decisions 1.4 and 4.3).
    /// </summary>
    public static IReadOnlySet<string> MailSchemes { get; } =
        new HashSet<string>(["smtp", "imap", "pop3"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The schemes this account may log in on, from <c>protocols=</c> (ADR-0102, decision 4.3);
    /// <see langword="null"/>, the default, means every scheme, as a <c>--user</c> or
    /// <c>--user-file</c> account does. Each scheme admits its TLS twin, matched without regard to case.
    /// </summary>
    public IReadOnlySet<string>? Schemes { get; init; }

    /// <summary>
    /// Whether this account may log in on <paramref name="scheme"/>: always when
    /// <see cref="Schemes"/> is <see langword="null"/>; otherwise when <see cref="Schemes"/> names
    /// the scheme or its TLS twin, the same name with or without a final <c>s</c>
    /// (<c>imap</c> and <c>imaps</c>, <c>ws</c> and <c>wss</c>; ADR-0102, decision 4.3).
    /// </summary>
    /// <param name="scheme">The scheme a server answers, compared without regard to case.</param>
    /// <returns><see langword="true"/> when a login on <paramref name="scheme"/> may reach this account.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scheme"/> is <see langword="null"/>.</exception>
    public bool MayLogInOn(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        return SchemesAdmit(Schemes, scheme);
    }

    /// <summary>
    /// Whether an account restricted to <paramref name="schemes"/> may log in on
    /// <paramref name="scheme"/>, as <see cref="MayLogInOn"/> decides.
    /// </summary>
    /// <param name="schemes">The account's schemes; <see langword="null"/> for every scheme.</param>
    /// <param name="scheme">The scheme a server answers.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    internal static bool SchemesAdmit(IReadOnlySet<string>? schemes, string scheme) =>
        schemes is null || schemes.Any(named => AreTlsTwinsOrEqual(named, scheme));

    /// <summary>
    /// Names the account without its password, so no log or exception text can hold one.
    /// </summary>
    /// <returns><c>Account { UserName = &lt;name&gt; }</c>.</returns>
    public override string ToString() => $"Account {{ UserName = {UserName} }}";

    private static bool AreTlsTwinsOrEqual(string named, string scheme) =>
        string.Equals(named, scheme, StringComparison.OrdinalIgnoreCase)
        || string.Equals(named + "s", scheme, StringComparison.OrdinalIgnoreCase)
        || string.Equals(named, scheme + "s", StringComparison.OrdinalIgnoreCase);
}
