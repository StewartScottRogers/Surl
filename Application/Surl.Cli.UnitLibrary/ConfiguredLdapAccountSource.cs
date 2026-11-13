namespace Surl.Cli;

/// <summary>
/// The <c>--user-ldap &lt;ldap-url&gt;[;bind-dn=&lt;dn&gt;][;bind-password-file=&lt;file&gt;][;ca-file=&lt;file&gt;][;timeout=&lt;seconds&gt;][;protocols=&lt;scheme&gt;,...][;plaintext]</c>
/// item (ADR-0149 decision 3): the LDAP directory password logins are checked against beside the
/// accounts. Read by <see cref="ConfiguredLdapAccountSourceReader"/>; the record holds what was written,
/// percent-decoded once by the item grammar. The URL's structure is checked by <c>Surl.Composition</c>
/// (ADR-0149 decision 7), because its parser lives in <c>Surl.Authentication</c>.
/// </summary>
/// <param name="Url">The URL as written, <c>ldap://</c> or <c>ldaps://</c>.</param>
/// <param name="BindDn">The DN <c>surl</c> binds as before searching; <see langword="null"/> searches anonymously.</param>
/// <param name="BindPasswordFile">The file whose first line is <paramref name="BindDn"/>'s password; given exactly when <paramref name="BindDn"/> is.</param>
/// <param name="CaFile">The PEM certificates trusted beside the system's roots; <see langword="null"/> for the system's roots alone.</param>
/// <param name="TimeoutSeconds">The seconds one login's whole directory check may take, 1 to 60.</param>
/// <param name="Protocols">The schemes, lower-case and each once, a directory account may log in on; <see langword="null"/> for every scheme.</param>
/// <param name="IsPlaintext">Whether <c>ldap://</c> is used without StartTLS (<c>;plaintext</c>).</param>
public sealed record ConfiguredLdapAccountSource(
    string Url,
    string? BindDn,
    string? BindPasswordFile,
    string? CaFile,
    int TimeoutSeconds,
    IReadOnlyList<string>? Protocols,
    bool IsPlaintext)
{
    /// <summary>
    /// Names the source by its scheme only, never the URL, the bind DN or a file (ADR-0149 decision 7),
    /// so no log or exception text can hold them.
    /// </summary>
    /// <returns><c>ConfiguredLdapAccountSource</c>.</returns>
    public override string ToString() => nameof(ConfiguredLdapAccountSource);
}
