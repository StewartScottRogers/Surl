using Surl.Output;

namespace Surl.Api;

/// <summary>
/// What <see cref="SurlServer.StartAsync(SurlServerOptions, CancellationToken)"/> starts: the listen
/// URLs, the content, any other <c>surl</c> option, and what the host provides (ADR-0099, decision 4).
/// Every command-line property is a spelling of the <c>surl</c> option it names, and the options are
/// parsed as <c>surl</c> parses them, so every default is <c>surl</c>'s and the start is validated
/// exactly as <c>surl</c> validates it.
/// </summary>
public sealed class SurlServerOptions
{
    /// <summary>
    /// The listen URLs, as <c>surl</c> takes them (<c>http://127.0.0.1:0</c>,
    /// <c>ftp://127.0.0.1:0</c>), in order; port 0 asks for an ephemeral port. At least one is needed.
    /// </summary>
    public IList<string> ListenUrls { get; } = [];

    /// <summary>
    /// The data directory served and its service state kept in (<c>--directory</c>, ADR-0031), or
    /// <see langword="null"/> to serve <see cref="Files"/> in memory. Not set together with
    /// <see cref="Files"/>.
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// The files served in memory when no <see cref="DataDirectory"/> is given: each key a path
    /// relative to the served root, its segments separated by <c>/</c>, each value the file's bytes.
    /// A path may not be empty, start with <c>/</c>, hold an empty, <c>.</c> or <c>..</c> segment, a
    /// <c>\</c> or a <c>:</c>, or name a directory another path is inside.
    /// </summary>
    public IDictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);

    /// <summary>Accepts uploads into served files (<c>--allow-uploads</c>); off by default.</summary>
    public bool AllowUploads { get; set; }

    /// <summary>Answers directory listings (<c>--list-directories</c>); off by default.</summary>
    public bool ListDirectories { get; set; }

    /// <summary>Follows links that stay in the served root (<c>--follow-symlinks</c>); off by default.</summary>
    public bool FollowSymlinks { get; set; }

    /// <summary>Serves names that start with a dot (<c>--serve-dot-files</c>); off by default.</summary>
    public bool ServeDotFiles { get; set; }

    /// <summary>
    /// Any other <c>surl</c> option, as <c>surl</c>'s own words, read after the properties above.
    /// <c>--help</c>, <c>--aihelp</c>, <c>--manual</c>, <c>--version</c>, <c>--log-file</c>,
    /// <c>--trace</c> and <c>--trace-ascii</c> belong to a host and are refused.
    /// </summary>
    public IList<string> AdditionalArguments { get; } = [];

    /// <summary>
    /// Where the start-up warnings, the status lines and the exchange log are written, at
    /// <see cref="LogLevel"/>, byte for byte as <c>surl</c> writes them; <see langword="null"/>, the
    /// default, writes nothing.
    /// </summary>
    public TextWriter? Log { get; set; }

    /// <summary>
    /// The level <see cref="Log"/> is written at (<c>--log-level</c>, ADR-0033);
    /// <see cref="Output.LogLevel.Info"/> by default.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    /// <summary>The one clock every exchange runs on; <see cref="TimeProvider.System"/> by default.</summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public TimeProvider TimeProvider
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = TimeProvider.System;

    /// <summary>
    /// Whether the extended protocol servers are served by this server (ADR-0083, decision 6), or
    /// <see langword="null"/>, the default, to read the process's <c>SURL_EXTENSIONS</c> at start.
    /// </summary>
    public bool? AreExtensionsTurnedOn { get; set; }
}
