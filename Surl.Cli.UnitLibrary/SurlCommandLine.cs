using System.Security.Authentication;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// A command line that asks <c>surl</c> to serve: the listen URLs and every Phase 1 option
/// value, with ADR-0007 section 2's default for each option not given (ADR-0031's for
/// <c>--directory</c>). A new instance holds only the defaults and no listen URL.
/// </summary>
/// <remarks>
/// Seconds are <see cref="TimeSpan"/>s and 0 seconds is <see cref="Timeout.InfiniteTimeSpan"/>;
/// a byte or connection count of 0 is no limit (ADR-0006 section 1).
/// </remarks>
public sealed record SurlCommandLine
{
    /// <summary>The listen URLs, in command-line order.</summary>
    public IReadOnlyList<ListenUrl> ListenUrls { get; init; } = [];

    /// <summary>
    /// The data directory as given with <c>--directory</c>; <see langword="null"/> by default,
    /// when surl serves an in-memory file system instead (ADR-0031 decisions 1 and 4).
    /// </summary>
    public string? DataDirectory { get; init; }

    /// <summary>
    /// The log level (ADR-0033 section 2): set by the last of <c>-s</c>, <c>--no-silent</c>,
    /// <c>-v</c>, <c>--no-verbose</c>, <c>--log-level</c>, <c>--trace</c> and
    /// <c>--trace-ascii</c>, then raised from <see cref="LogLevel.None"/> to
    /// <see cref="LogLevel.Error"/> by <see cref="ShowError"/> once the whole line is read.
    /// <see cref="LogLevel.Info"/> by default.
    /// </summary>
    public LogLevel LogLevel { get; init; } = LogLevel.Info;

    /// <summary>
    /// <c>-S</c>/<c>--show-error</c>: whether it was given (and not reversed). The parser has
    /// already applied it to <see cref="LogLevel"/>. Off by default.
    /// </summary>
    public bool ShowError { get; init; }

    /// <summary>
    /// The file of the last <c>--trace</c> or <c>--trace-ascii</c>, as given (<c>-</c> is
    /// stdout), when <see cref="LogLevel"/> is <see cref="LogLevel.Trace"/>; otherwise, and
    /// by default, none.
    /// </summary>
    public string? TraceFile { get; init; }

    /// <summary>
    /// The layout of the last <c>--trace</c> (<see cref="TraceDumpLayout.HexAndAscii"/>) or
    /// <c>--trace-ascii</c> (<see cref="TraceDumpLayout.Ascii"/>);
    /// <see cref="TraceDumpLayout.HexAndAscii"/> by default, as <c>--log-level trace</c>
    /// alone dumps.
    /// </summary>
    public TraceDumpLayout TraceLayout { get; init; } = TraceDumpLayout.HexAndAscii;

    /// <summary><c>--trace-time</c>: stamp each exchange log line with the local time. Off by default.</summary>
    public bool TraceTime { get; init; }

    /// <summary>
    /// <c>--log-file</c>: the file the log stream is appended to, as given (<c>-</c> is
    /// stdout); none by default, when the log stream is stderr.
    /// </summary>
    public string? LogFile { get; init; }

    /// <summary><c>--allow-uploads</c>: accept uploads into the served files. Off by default.</summary>
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
    /// <c>--pass</c>: the passphrase for an encrypted <see cref="KeyFile"/>, a PKCS#12
    /// <see cref="CertificateFile"/> or an encrypted <see cref="HostKeyFiles"/> key (ADR-0051
    /// decision 4), as given, the empty string included; none by default.
    /// </summary>
    public string? KeyPassphrase { get; init; }

    /// <summary>
    /// <c>--cacert</c>: the file of trust anchors that verify a client certificate, as given;
    /// none by default.
    /// </summary>
    public string? CaCertificateFile { get; init; }

    /// <summary>
    /// The accounts from every <c>-u</c>/<c>--user</c>, in command-line order: each one adds an
    /// account, and no user name is given twice (ADR-0032 section 1). None by default.
    /// </summary>
    public IReadOnlyList<CommandLineAccount> Accounts { get; init; } = [];

    /// <summary>
    /// <c>--user-file</c>: the file of more accounts, as given; none by default. It is read
    /// when surl starts serving, not while parsing (ADR-0032 section 2).
    /// </summary>
    public string? UserFile { get; init; }

    /// <summary>
    /// <c>--allow-anonymous</c>: accept every request and login without checking credentials.
    /// Off by default.
    /// </summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>
    /// <c>--allow-plaintext-auth</c>: accept passwords and tokens over unencrypted connections.
    /// Off by default.
    /// </summary>
    public bool AllowPlaintextAuthentication { get; init; }

    /// <summary>
    /// The <c>--auth</c> words of the last <c>--auth</c>, lower-case and in ADR-0032 section 3's
    /// order; <see langword="null"/> when <c>--auth</c> was not given.
    /// </summary>
    public IReadOnlyList<string>? GivenAuthenticationMethods { get; init; }

    /// <summary>
    /// The authentication methods accepted: <see cref="GivenAuthenticationMethods"/>, or
    /// ADR-0049 section 3's default set (<c>digest</c>, <c>cram-md5</c>, <c>basic</c>, <c>plain</c>,
    /// <c>login</c>, <c>bearer</c>, <c>oauthbearer</c>, <c>xoauth2</c> and <c>aws-sigv4</c>) when
    /// <c>--auth</c> was not given (ADR-0032 section 1), in ADR-0032 section 3's order.
    /// </summary>
    public IReadOnlyList<string> AcceptedAuthenticationMethods => GivenAuthenticationMethods ?? DefaultAuthenticationMethods;

    /// <summary>
    /// <c>--self-signed</c>: serve a throwaway self-signed certificate when no <c>--cert</c> is
    /// given. Off by default.
    /// </summary>
    public bool SelfSigned { get; init; }

    /// <summary>
    /// The SSH host private key files from every <c>--hostkey</c>, as given, in command-line order:
    /// each one adds a key (ADR-0051 decision 4). None by default.
    /// </summary>
    public IReadOnlyList<string> HostKeyFiles { get; init; } = [];

    /// <summary>
    /// The OpenSSH host certificate files from every <c>--hostcert</c>, as given, in command-line
    /// order (ADR-0051 decision 4). None by default.
    /// </summary>
    public IReadOnlyList<string> HostCertificateFiles { get; init; } = [];

    /// <summary>
    /// <c>--throwaway-hostkey</c>: make a throwaway RSA host key at start when an <c>scp</c> or
    /// <c>sftp</c> URL is served and no <c>--hostkey</c> is given (ADR-0051 decision 4). Off by default.
    /// </summary>
    public bool ThrowawayHostKey { get; init; }

    /// <summary>
    /// The users and <c>authorized_keys</c> files from every <c>--authorized-keys</c>, in
    /// command-line order; no user name is given twice (ADR-0051 decision 6). None by default.
    /// </summary>
    public IReadOnlyList<CommandLineAuthorizedKeys> AuthorizedKeys { get; init; } = [];

    /// <summary>
    /// <c>--allow-weak-ssh-algorithms</c>: also offer ADR-0051 decision 2's weak SSH algorithms.
    /// Off by default.
    /// </summary>
    public bool AllowWeakSshAlgorithms { get; init; }

    /// <summary>The methods accepted without <c>--auth</c>, in ADR-0032 section 3's order.</summary>
    private static readonly IReadOnlyList<string> DefaultAuthenticationMethods =
        ["digest", "cram-md5", "basic", "plain", "login", "bearer", "oauthbearer", "xoauth2", "aws-sigv4"];
}
