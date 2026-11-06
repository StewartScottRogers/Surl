namespace Surl.Cli;

/// <summary>
/// One <c>--authorized-keys &lt;user:file&gt;</c> (ADR-0051 decision 6): the value split at its
/// first <c>:</c>, so a Windows path after it keeps its drive colon. The file is OpenSSH's
/// <c>authorized_keys</c> format, read by <c>Surl.Console</c> when surl starts serving, not while
/// parsing.
/// </summary>
/// <param name="UserName">The account name the keys log in as; never empty, never holding a control character.</param>
/// <param name="File">The <c>authorized_keys</c> file, as given; never empty.</param>
public sealed record CommandLineAuthorizedKeys(string UserName, string File);
