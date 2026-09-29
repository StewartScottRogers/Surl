using System.Security.Authentication;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// A command line that asks <c>surl</c> to serve: the listen URLs and every Phase 1 option
/// value, with ADR-0007 section 2's default for each option not given. A new instance holds
/// only the defaults and no listen URL.
/// </summary>
/// <remarks>
/// Seconds are <see cref="TimeSpan"/>s and 0 seconds is <see cref="Timeout.InfiniteTimeSpan"/>;
/// a byte or connection count of 0 is no limit (ADR-0006 section 1).
/// </remarks>
public sealed record SurlCommandLine
{
    /// <summary>The listen URLs, in command-line order.</summary>
    public IReadOnlyList<ListenUrl> ListenUrls { get; init; } = [];

    /// <summary>The served directory as given with <c>--directory</c>; <c>.</c> by default.</summary>
    public string ServedDirectory { get; init; } = ".";

    /// <summary><c>-v</c>/<c>--verbose</c>: write the verbose exchange log to stderr. Off by default.</summary>
    public bool Verbose { get; init; }

    /// <summary><c>--allow-uploads</c>: accept uploads into the served directory. Off by default.</summary>
    public bool AllowUploads { get; init; }

    /// <summary><c>--list-directories</c>: answer directory listings. Off by default.</summary>
    public bool ListDirectories { get; init; }

    /// <summary>
    /// <c>--follow-symlinks</c>: follow a link whose final target stays inside the served
    /// directory. Off by default.
    /// </summary>
    public bool FollowSymlinks { get; init; }

    /// <summary><c>--serve-dot-files</c>: serve paths with a segment starting with <c>.</c>. Off by default.</summary>
    public bool ServeDotFiles { get; init; }

    /// <summary>
    /// <c>--max-connections</c>: concurrent connections and datagram flows, all listeners
    /// together; 0 is no limit. 1024 by default.
    /// </summary>
    public int MaxConnections { get; init; } = 1024;

    /// <summary>
    /// <c>--max-connections-per-address</c>: concurrent connections and flows from one remote
    /// IP address; 0 is no limit. 100 by default.
    /// </summary>
    public int MaxConnectionsPerAddress { get; init; } = 100;

    /// <summary><c>--idle-timeout</c>: close an exchange after this long with no byte moving. 120 seconds by default.</summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary><c>-m</c>/<c>--max-time</c>: the longest one exchange may last. 3600 seconds by default.</summary>
    public TimeSpan MaxTime { get; init; } = TimeSpan.FromSeconds(3600);

    /// <summary>
    /// <c>--head-timeout</c>: the time a peer has to deliver a complete request head, command
    /// line or first packet. 30 seconds by default.
    /// </summary>
    public TimeSpan HeadTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary><c>--max-request-head</c>: the largest HTTP/1.x or RTSP request head, in bytes. 102400 by default.</summary>
    public long MaxRequestHeadBytes { get; init; } = 102400;

    /// <summary>
    /// <c>--max-line</c>: the longest command line of a line-oriented protocol, line ending
    /// included, in bytes. 8192 by default.
    /// </summary>
    public long MaxLineBytes { get; init; } = 8192;

    /// <summary><c>--max-message</c>: the largest framed message of a binary protocol, in bytes. 1048576 by default.</summary>
    public long MaxMessageBytes { get; init; } = 1048576;

    /// <summary><c>--max-filesize</c>: the largest upload accepted, in bytes. 104857600 by default.</summary>
    public long MaxUploadBytes { get; init; } = 104857600;

    /// <summary>
    /// The lowest TLS version accepted, set by the last of <c>--tlsv1.0</c> to <c>--tlsv1.3</c>;
    /// <see cref="SslProtocols.Tls12"/> by default.
    /// </summary>
    public SslProtocols LowestTlsVersion { get; init; } = SslProtocols.Tls12;

    /// <summary><c>--tls-max</c>: the highest TLS version accepted; <see cref="SslProtocols.Tls13"/> by default.</summary>
    public SslProtocols HighestTlsVersion { get; init; } = SslProtocols.Tls13;

    /// <summary><c>--cert</c>: the server certificate file for secure schemes, as given; none by default.</summary>
    public string? CertificateFile { get; init; }

    /// <summary><c>--key</c>: the private key file for <see cref="CertificateFile"/>, as given; none by default.</summary>
    public string? KeyFile { get; init; }

    /// <summary>
    /// <c>--cacert</c>: the file of trust anchors that verify a client certificate, as given;
    /// none by default.
    /// </summary>
    public string? CaCertificateFile { get; init; }
}
