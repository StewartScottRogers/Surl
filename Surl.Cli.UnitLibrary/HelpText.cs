namespace Surl.Cli;

/// <summary>
/// The text <c>surl --help</c> writes to stdout, exactly as ADR-0007 section 6 gives it:
/// options alphabetical by long name, every description starting in column 46.
/// </summary>
public static class HelpText
{
    private static readonly string[] Lines =
    [
        "Usage: surl [options...] <url>...",
        "     --allow-uploads                         Accept uploads into the served directory",
        "     --cacert <file>                         CA certificates that verify client certificates",
        "     --cert <file>                           Server certificate for secure schemes",
        "     --directory <directory>                 Directory to serve (default: current directory)",
        "     --follow-symlinks                       Follow links that stay inside the directory",
        "     --head-timeout <seconds>                Time a peer has to send a request head (default 30)",
        " -h, --help                                  Show this help and quit",
        "     --idle-timeout <seconds>                Close an exchange idle this long (default 120)",
        "     --key <file>                            Private key for --cert",
        "     --list-directories                      Answer directory listings",
        "     --max-connections <number>              Connections at once, all listeners (default 1024)",
        "     --max-connections-per-address <number>  Connections at once from one address (default 100)",
        "     --max-filesize <bytes>                  Largest upload accepted (default 100M)",
        "     --max-line <bytes>                      Longest command line accepted (default 8192)",
        "     --max-message <bytes>                   Largest framed message accepted (default 1M)",
        "     --max-request-head <bytes>              Largest HTTP or RTSP request head (default 100k)",
        " -m, --max-time <seconds>                    Longest time one exchange may take (default 3600)",
        "     --serve-dot-files                       Serve names that start with a dot",
        "     --tls-max <version>                     Highest TLS version accepted (default 1.3)",
        "     --tlsv1.0                               Accept TLS 1.0 or later",
        "     --tlsv1.1                               Accept TLS 1.1 or later",
        "     --tlsv1.2                               Accept TLS 1.2 or later (default)",
        "     --tlsv1.3                               Accept TLS 1.3 or later",
        " -v, --verbose                               Log every exchange to stderr",
        " -V, --version                               Show version number and quit",
    ];

    /// <summary>
    /// The whole help: every line ending with <see cref="Environment.NewLine"/>, ready to
    /// write to stdout as it is.
    /// </summary>
    public static string Text { get; } = string.Concat(Lines.Select(line => line + Environment.NewLine));
}
