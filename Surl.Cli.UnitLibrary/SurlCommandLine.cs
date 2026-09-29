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
    /// The per-exchange limits (ADR-0007 section 3): <c>--head-timeout</c> sets
    /// <see cref="ExchangeLimits.HeadTimeout"/>, <c>--max-request-head</c>
    /// <see cref="ExchangeLimits.MaxRequestHeadBytes"/>, <c>--max-line</c>
    /// <see cref="ExchangeLimits.MaxLineBytes"/>, <c>--max-message</c>
    /// <see cref="ExchangeLimits.MaxMessageBytes"/> and <c>--max-filesize</c>
    /// <see cref="ExchangeLimits.MaxUploadBytes"/>. <see cref="ExchangeLimits.Default"/> by default.
    /// </summary>
    public ExchangeLimits Limits { get; init; } = ExchangeLimits.Default;

    /// <summary>
    /// The lowest TLS version accepted, set by the last of <c>--tlsv1.0</c> to <c>--tlsv1.3</c>;
    /// <see cref="SslProtocols.Tls12"/> by default.
    /// </summary>
    public SslProtocols LowestTlsVersion { get; init; } = SslProtocols.Tls12;

    /// <summary><c>--tls-max</c>: the highest TLS version accepted; <see cref="SslProtocols.Tls13"/> by default.</summary>
    public SslProtocols HighestTlsVersion { get; init; } = SslProtocols.Tls13;

    /// <summary><c>--cert</c>: the server certificate file for secure schemes, as given; none by default.</summary>
    public string? CertificateFile { get; init; }

    /// <summary><c>--cert-type</c>: the format of <see cref="CertificateFile"/>; <see cref="CertificateFileFormat.Pem"/> by default.</summary>
    public CertificateFileFormat CertificateType { get; init; } = CertificateFileFormat.Pem;

    /// <summary><c>--key</c>: the private key file for <see cref="CertificateFile"/>, as given; none by default.</summary>
    public string? KeyFile { get; init; }

    /// <summary><c>--key-type</c>: the format of <see cref="KeyFile"/>; <see cref="CertificateFileFormat.Pem"/> by default.</summary>
    public CertificateFileFormat KeyType { get; init; } = CertificateFileFormat.Pem;

    /// <summary>
    /// <c>--pass</c>: the passphrase for an encrypted <see cref="KeyFile"/> or a PKCS#12
    /// <see cref="CertificateFile"/>, as given, the empty string included; none by default.
    /// </summary>
    public string? KeyPassphrase { get; init; }

    /// <summary>
    /// <c>--cacert</c>: the file of trust anchors that verify a client certificate, as given;
    /// none by default.
    /// </summary>
    public string? CaCertificateFile { get; init; }
}
