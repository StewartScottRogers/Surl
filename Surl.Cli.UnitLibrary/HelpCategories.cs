using System.Diagnostics.CodeAnalysis;

namespace Surl.Cli;

/// <summary>ADR-0034 decision 1's help categories, in ordinal order of their names.</summary>
internal static class HelpCategories
{
    private static readonly HelpCategory[] Table =
    [
        new("auth", "Accounts and authentication methods", []),
        new("content", "Served files and the data directory", []),
        new("dict", "DICT protocol", ["dict"]),
        new("ftp", "FTP and FTPS protocol", ["ftp", "ftps"]),
        new("gopher", "GOPHER and GOPHERS protocol", ["gopher", "gophers"]),
        new("http", "HTTP and HTTPS protocol", ["http", "https"]),
        new("imap", "IMAP and IMAPS protocol", ["imap", "imaps"]),
        new("limits", "Connection, time and size limits", []),
        new("logging", "Log levels, tracing and the log file", []),
        new("mqtt", "MQTT and MQTTS protocol", ["mqtt", "mqtts"]),
        new("pop3", "POP3 and POP3S protocol", ["pop3", "pop3s"]),
        new("rtsp", "RTSP protocol", ["rtsp"]),
        new("security", "Options that widen what a peer may do", []),
        new("smb", "SMB and SMBS protocol", ["smb", "smbs"]),
        new("smtp", "SMTP and SMTPS protocol", ["smtp", "smtps"]),
        new("ssh", "SSH protocol", ["scp", "sftp"]),
        new("surl", "The command line tool itself", []),
        new("telnet", "TELNET protocol", ["telnet"]),
        new("testing", "Loosening options for tests (warned)", []),
        new("tftp", "TFTP protocol", ["tftp"]),
        new("tls", "TLS certificates and versions", []),
        new("websocket", "WebSocket protocol", ["ws", "wss"]),
    ];

    /// <summary>Every category, in ordinal order of its name.</summary>
    public static IReadOnlyList<HelpCategory> All => Table;

    /// <summary>Finds a category by its name, in any case, as curl matches its categories.</summary>
    /// <param name="name">The name as given (<c>TLS</c>).</param>
    /// <param name="category">The category, when found.</param>
    /// <returns><see langword="true"/> when a category has the name.</returns>
    public static bool TryFind(string name, [NotNullWhen(true)] out HelpCategory? category)
    {
        category = Array.Find(Table, candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return category is not null;
    }
}
