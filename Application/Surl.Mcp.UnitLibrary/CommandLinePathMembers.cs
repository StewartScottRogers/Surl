using System.Collections.Frozen;
using Surl.Cli;

namespace Surl.Mcp;

/// <summary>
/// Which <see cref="SurlCommandLine"/> members hold a local path and which do not (ADR-0103 decision 4). Every
/// member is in exactly one of <see cref="PathMembers"/> and <see cref="MembersWithoutPaths"/>, and a test fails on
/// a member neither names, so a new path option cannot slip past the root check.
/// </summary>
internal static class CommandLinePathMembers
{
    /// <summary>Each member that holds local paths, with the option a refusal names and how its paths are read.</summary>
    public static IReadOnlyList<CommandLinePathMember> PathMembers { get; } =
    [
        new(nameof(SurlCommandLine.ListenUrls), "a Unix domain socket listen URL", commandLine => commandLine.ListenUrls
            .Where(listenUrl => listenUrl.UnixSocket is { IsAbstract: false })
            .Select(listenUrl => listenUrl.UnixSocket!.Name)),
        new(nameof(SurlCommandLine.DataDirectory), "--directory", commandLine => OneOrNone(commandLine.DataDirectory)),
        new(nameof(SurlCommandLine.TraceFile), "--trace", commandLine => OneOrNone(commandLine.TraceFile)),
        new(nameof(SurlCommandLine.LogFile), "--log-file", commandLine => OneOrNone(commandLine.LogFile)),
        new(nameof(SurlCommandLine.CertificateFile), "--cert", commandLine => OneOrNone(commandLine.CertificateFile)),
        new(nameof(SurlCommandLine.KeyFile), "--key", commandLine => OneOrNone(commandLine.KeyFile)),
        new(nameof(SurlCommandLine.CaCertificateFile), "--cacert", commandLine => OneOrNone(commandLine.CaCertificateFile)),
        new(nameof(SurlCommandLine.UserFile), "--user-file", commandLine => OneOrNone(commandLine.UserFile)),
        new(nameof(SurlCommandLine.KeytabFile), "--keytab", commandLine => OneOrNone(commandLine.KeytabFile)),
        new(nameof(SurlCommandLine.HostKeyFiles), "--hostkey", commandLine => commandLine.HostKeyFiles),
        new(nameof(SurlCommandLine.HostCertificateFiles), "--hostcert", commandLine => commandLine.HostCertificateFiles),
        new(nameof(SurlCommandLine.AuthorizedKeys), "--authorized-keys", commandLine => commandLine.AuthorizedKeys.Select(keys => keys.File)),
    ];

    /// <summary>Every member that holds no local path.</summary>
    public static FrozenSet<string> MembersWithoutPaths { get; } = FrozenSet.ToFrozenSet(
    [
        nameof(SurlCommandLine.LogLevel),
        nameof(SurlCommandLine.ShowError),
        nameof(SurlCommandLine.TraceLayout),
        nameof(SurlCommandLine.TraceTime),
        nameof(SurlCommandLine.AllowUploads),
        nameof(SurlCommandLine.ListDirectories),
        nameof(SurlCommandLine.FollowSymlinks),
        nameof(SurlCommandLine.ServeDotFiles),
        nameof(SurlCommandLine.MaxConnections),
        nameof(SurlCommandLine.MaxConnectionsPerAddress),
        nameof(SurlCommandLine.IdleTimeout),
        nameof(SurlCommandLine.MaxTime),
        nameof(SurlCommandLine.Limits),
        nameof(SurlCommandLine.LowestTlsVersion),
        nameof(SurlCommandLine.HighestTlsVersion),
        nameof(SurlCommandLine.CertificateType),
        nameof(SurlCommandLine.KeyType),
        nameof(SurlCommandLine.KeyPassphrase),
        nameof(SurlCommandLine.Accounts),
        nameof(SurlCommandLine.Groups),
        nameof(SurlCommandLine.AllowAnonymous),
        nameof(SurlCommandLine.AllowPlaintextAuthentication),
        nameof(SurlCommandLine.GivenAuthenticationMethods),
        nameof(SurlCommandLine.AcceptedAuthenticationMethods),
        nameof(SurlCommandLine.SelfSigned),
        nameof(SurlCommandLine.ThrowawayHostKey),
        nameof(SurlCommandLine.AllowWeakSshAlgorithms),
        nameof(SurlCommandLine.SshCiphers),
        nameof(SurlCommandLine.SshMacs),
        nameof(SurlCommandLine.WsEcho),
        nameof(SurlCommandLine.Http3),
    ], StringComparer.Ordinal);

    /// <summary>Every local path <paramref name="commandLine"/> names, each with the option that names it, in table order.</summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>Each option and path.</returns>
    public static IEnumerable<(string OptionName, string Path)> PathsOf(SurlCommandLine commandLine) =>
        PathMembers.SelectMany(member => member.ReadPaths(commandLine).Select(path => (member.OptionName, path)));

    private static IEnumerable<string> OneOrNone(string? path) => path is null ? [] : [path];
}
