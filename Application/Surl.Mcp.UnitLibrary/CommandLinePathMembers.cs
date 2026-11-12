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
        new(nameof(SurlCommandLine.SrpVerifierFile), "--srp-verifiers", commandLine => OneOrNone(commandLine.SrpVerifierFile)),
        new(nameof(SurlCommandLine.SrpGroupFile), "--srp-groups", commandLine => OneOrNone(commandLine.SrpGroupFile)),
        new(nameof(SurlCommandLine.UserFile), "--user-file", commandLine => OneOrNone(commandLine.UserFile)),
        new(nameof(SurlCommandLine.ProxyUserFile), "--proxy-user-file", commandLine => OneOrNone(commandLine.ProxyUserFile)),
        new(nameof(SurlCommandLine.KeytabFile), "--keytab", commandLine => OneOrNone(commandLine.KeytabFile)),
        new(nameof(SurlCommandLine.HostKeyFiles), "--hostkey", commandLine => commandLine.HostKeyFiles),
        new(nameof(SurlCommandLine.HostCertificateFiles), "--hostcert", commandLine => commandLine.HostCertificateFiles),
        new(nameof(SurlCommandLine.AuthorizedKeys), "--authorized-keys", commandLine => commandLine.AuthorizedKeys.Select(keys => keys.File)),
        new(nameof(SurlCommandLine.MailboxFiles), "--mailbox-file", commandLine => commandLine.MailboxFiles),
        new(nameof(SurlCommandLine.Shares), "--share", commandLine => commandLine.Shares.Select(share => share.Path)),
        new(nameof(SurlCommandLine.ControlTokenFile), "--control-token-file", commandLine => OneOrNone(commandLine.ControlTokenFile)),
        new(nameof(SurlCommandLine.ConfigFiles), "--config", commandLine => commandLine.ConfigFiles),
    ];

    /// <summary>
    /// Every member that holds no local path, or none the root check does not already read through another member:
    /// <see cref="SurlCommandLine.UnixSocket"/>'s socket file is also set on its listen URL, which
    /// <see cref="SurlCommandLine.ListenUrls"/>' row reads.
    /// </summary>
    public static FrozenSet<string> MembersWithoutPaths { get; } = FrozenSet.ToFrozenSet(
    [
        nameof(SurlCommandLine.UnixSocket),
        nameof(SurlCommandLine.LogLevel),
        nameof(SurlCommandLine.ShowError),
        nameof(SurlCommandLine.TraceLayout),
        nameof(SurlCommandLine.TraceTime),
        nameof(SurlCommandLine.AllowUploads),
        nameof(SurlCommandLine.ListDirectories),
        nameof(SurlCommandLine.FollowSymlinks),
        nameof(SurlCommandLine.ServeDotFiles),
        nameof(SurlCommandLine.AllowZoneTransfer),
        nameof(SurlCommandLine.DnsTransports),
        nameof(SurlCommandLine.DnsListenUrlsWithPort),
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
        nameof(SurlCommandLine.TlsAuthenticationType),
        nameof(SurlCommandLine.TlsUser),
        nameof(SurlCommandLine.TlsPassword),
        nameof(SurlCommandLine.ServesTlsSrp),
        nameof(SurlCommandLine.Accounts),
        nameof(SurlCommandLine.ProxyListenUrls),
        nameof(SurlCommandLine.ProxyAccounts),
        nameof(SurlCommandLine.ProxyAllowedOrigins),
        nameof(SurlCommandLine.Groups),
        nameof(SurlCommandLine.MailDomains),
        nameof(SurlCommandLine.Mailboxes),
        nameof(SurlCommandLine.MailAliases),
        nameof(SurlCommandLine.MailCatchAlls),
        nameof(SurlCommandLine.SharedFolders),
        nameof(SurlCommandLine.ShareAccess),
        nameof(SurlCommandLine.Buckets),
        nameof(SurlCommandLine.Topics),
        nameof(SurlCommandLine.Queues),
        nameof(SurlCommandLine.TopicMaps),
        nameof(SurlCommandLine.TopicAccess),
        nameof(SurlCommandLine.Faults),
        nameof(SurlCommandLine.ControlListenUrl),
        nameof(SurlCommandLine.RandomSeed),
        nameof(SurlCommandLine.AllowAnonymous),
        nameof(SurlCommandLine.AllowPlaintextAuthentication),
        nameof(SurlCommandLine.GivenAuthenticationMethods),
        nameof(SurlCommandLine.AcceptedAuthenticationMethods),
        nameof(SurlCommandLine.GivenProxyAuthenticationMethods),
        nameof(SurlCommandLine.AcceptedProxyAuthenticationMethods),
        nameof(SurlCommandLine.SelfSigned),
        nameof(SurlCommandLine.S3Plaintext),
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
