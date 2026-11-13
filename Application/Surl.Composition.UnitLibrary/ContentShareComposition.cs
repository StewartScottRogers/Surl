using Surl.Cli;
using Surl.Content;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Every <c>--share</c>, <c>--share-access</c> and <c>--bucket</c> composed into the shares and buckets of
/// the one content store every file and object-storage server shares (ADR-0102 decisions 2, 4 and 5):
/// each share's name checked, its path opened and checked against <c>--directory</c> and the other
/// shares, its <c>protocols=</c> widened to each TLS twin, and its grants expanded from groups to
/// account names, before any listener binds.
/// </summary>
public static class ContentShareComposition
{
    private const string WarningPrefix = "surl: warning: ";
    private const string ShareOption = "share";
    private const string ShareAccessOption = "share-access";
    private const string BucketOption = "bucket";

    /// <summary>
    /// Composes the shares and buckets of <paramref name="commandLine"/>, refusing, in this order: a
    /// <c>--share-access</c> member naming no account or group (ADR-0101 decision 5 row 18); then for each
    /// share in command-line order, a name that is not one valid path segment or is <c>.surl</c>
    /// (row 10), a path that cannot be opened as a directory (row 19, exit 37), and a path that equals,
    /// lies inside or holds <c>--directory</c>'s or an earlier share's (ADR-0102 decision 2.1); then a
    /// bucket whose name or content path a request may not name.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="groups">Every <c>--group</c>, expanded over the accounts.</param>
    /// <param name="canOpenShareDirectory">Tells whether a share's full path opens as a directory.</param>
    /// <param name="shareFileSystem">The file system every share is read and written through.</param>
    /// <returns>
    /// The shares and buckets in command-line order; or null, the exit code and the refusal after <c>surl: </c>.
    /// </returns>
    public static (IReadOnlyList<ContentShare>? Shares, IReadOnlyList<ContentBucket>? Buckets, SurlExitCode ExitCode, string? FailureMessage) Compose(
        SurlCommandLine commandLine,
        AccountGroupExpansion groups,
        Func<string, bool> canOpenShareDirectory,
        IContentFileSystem shareFileSystem)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(canOpenShareDirectory);
        ArgumentNullException.ThrowIfNull(shareFileSystem);
        var grantsByShare = new Dictionary<string, Dictionary<string, ContentShareAccess>>(StringComparer.Ordinal);
        if (ComposeGrants(commandLine.ShareAccess, groups, grantsByShare) is { } grantFailure)
        {
            return (null, null, SurlExitCode.FailedInit, grantFailure);
        }

        var shares = new List<ContentShare>();
        foreach (var configured in commandLine.Shares)
        {
            var (share, exitCode, failure) = ComposeShare(configured, commandLine.DataDirectory, shares, grantsByShare, canOpenShareDirectory, shareFileSystem);
            if (share is null)
            {
                return (null, null, exitCode, failure);
            }

            shares.Add(share);
        }

        var buckets = new List<ContentBucket>();
        foreach (var configured in commandLine.Buckets)
        {
            var (bucket, failure) = ComposeBucket(configured);
            if (bucket is null)
            {
                return (null, null, SurlExitCode.FailedInit, failure);
            }

            buckets.Add(bucket);
        }

        return (shares, buckets, SurlExitCode.Ok, null);
    }

    /// <summary>
    /// The schemes a share's <c>protocols=</c> admits: each scheme written and its TLS twin, <c>ftp</c>
    /// admitting <c>ftps</c> and <c>ftps</c> admitting <c>ftp</c> (ADR-0102 decisions 2.1 and 4.3), as
    /// <c>Account</c> compares a mailbox's schemes. A twin no server answers is harmless: no caller arrives on it.
    /// </summary>
    /// <param name="protocols">The schemes written; null when the share is not restricted.</param>
    /// <returns>The schemes with their twins, or null for every scheme.</returns>
    public static IReadOnlySet<string>? WithTlsTwins(IReadOnlyList<string>? protocols) =>
        protocols?
            .SelectMany(scheme => new[] { scheme, scheme + "s", scheme.EndsWith('s') ? scheme[..^1] : scheme })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Writes ADR-0102 decision 2.2's warning for each share granting <c>@everyone</c>, from the info level
    /// up: <c>surl: warning: --share-access: share &lt;name&gt; grants &lt;r|rw&gt; to every caller, anonymous included</c>.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <param name="contentStore">The composed content store.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteWarnings(LogLevel logLevel, ContentStore contentStore, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(log);
        if (logLevel < LogLevel.Info)
        {
            return;
        }

        foreach (var share in contentStore.Shares)
        {
            if (share.Grants?.TryGetValue(ContentShare.EveryoneMember, out var access) == true)
            {
                log.WriteLine(WarningPrefix + $"--{ShareAccessOption}: share {share.Name} grants {AccessWord(access)} to every caller, anonymous included");
            }
        }
    }

    /// <summary>
    /// Writes ADR-0102 decision 5's notes, from the verbose level up: <c>* Share &lt;name&gt;: &lt;full
    /// path&gt;[, read-only][, schemes &lt;schemes&gt;][, access &lt;n&gt; grants]</c> per share, then
    /// <c>* Bucket &lt;name&gt;: &lt;content path&gt;</c> per bucket, the schemes in ordinal order.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <param name="contentStore">The composed content store.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteStartLines(LogLevel logLevel, ContentStore contentStore, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(log);
        if (logLevel < LogLevel.Verbose)
        {
            return;
        }

        foreach (var share in contentStore.Shares)
        {
            var readOnly = share.IsReadOnly ? ", read-only" : string.Empty;
            var schemes = share.Schemes is { } named ? ", schemes " + string.Join(", ", named.Order(StringComparer.Ordinal)) : string.Empty;
            var access = share.Grants is { } grants ? $", access {grants.Count} grants" : string.Empty;
            log.WriteLine($"* Share {share.Name}: {share.Path}{readOnly}{schemes}{access}");
        }

        foreach (var bucket in contentStore.Buckets)
        {
            log.WriteLine($"* Bucket {bucket.Name}: {bucket.ContentPath}");
        }
    }

    /// <summary>A share whose name a request path may not hold (ADR-0101 decision 5 row 10).</summary>
    /// <param name="name">The share's name.</param>
    /// <returns><c>(2) option --share: share &lt;name&gt; is not a valid folder name</c>.</returns>
    public static string ShareNameIsNotAFolderName(string name) => $"(2) option --{ShareOption}: share {name} is not a valid folder name";

    /// <summary>A share whose path cannot be opened as a directory (ADR-0101 decision 5 row 19).</summary>
    /// <param name="path">The path as written.</param>
    /// <param name="name">The share's name.</param>
    /// <returns><c>(37) Could not open directory &lt;path&gt; for share &lt;name&gt;</c>.</returns>
    public static string ShareDirectoryCannotBeOpened(string path, string name) => $"(37) Could not open directory {path} for share {name}";

    /// <summary>A share whose path overlaps another's (ADR-0102 decision 2.1).</summary>
    /// <param name="name">The share's name.</param>
    /// <param name="other">The other share's name, or <c>--directory</c>.</param>
    /// <returns><c>(2) option --share: share &lt;name&gt; overlaps &lt;other&gt;</c>.</returns>
    public static string ShareOverlaps(string name, string other) => $"(2) option --{ShareOption}: share {name} overlaps {other}";

    /// <summary>A bucket whose name a request path may not hold (ADR-0101 decision 5 row 10).</summary>
    /// <param name="name">The bucket's name.</param>
    /// <returns><c>(2) option --bucket: bucket &lt;name&gt; is not a valid folder name</c>.</returns>
    public static string BucketNameIsNotAFolderName(string name) => $"(2) option --{BucketOption}: bucket {name} is not a valid folder name";

    /// <summary>A bucket whose content path a request may not name (ADR-0102 decision 2.4).</summary>
    /// <param name="name">The bucket's name.</param>
    /// <param name="path">The content path as written.</param>
    /// <returns><c>(2) option --bucket: bucket &lt;name&gt; maps to &lt;path&gt;, which is not a content path</c>.</returns>
    public static string BucketPathIsNotAContentPath(string name, string path) =>
        $"(2) option --{BucketOption}: bucket {name} maps to {path}, which is not a content path";

    /// <summary>A bucket whose folder is missing on disk, or cannot be created in memory (ADR-0102 decision 2.4).</summary>
    /// <param name="path">The content path as written.</param>
    /// <param name="name">The bucket's name.</param>
    /// <returns><c>(37) Could not open directory &lt;path&gt; for bucket &lt;name&gt;</c>.</returns>
    public static string BucketDirectoryCannotBeOpened(string path, string name) => $"(37) Could not open directory {path} for bucket {name}";

    /// <summary>
    /// Checks each bucket's folder at start (ADR-0102 decision 2.4): a content path that lies on disk, because
    /// its first segment names a share or <c>--directory</c> is given, must be an existing folder; one in the
    /// in-memory root is created empty, with any missing folder above it.
    /// </summary>
    /// <param name="commandLine">The parsed command line, whose buckets are checked in order.</param>
    /// <param name="contentStore">The composed content store, holding the shares and the buckets.</param>
    /// <param name="dataDirectoryFileSystem">The file system the data directory is read through.</param>
    /// <returns>Null when every folder is there; else the refusal after <c>surl: </c>, exit code 37.</returns>
    public static string? PrepareBucketFolders(
        SurlCommandLine commandLine, ContentStore contentStore, IContentFileSystem dataDirectoryFileSystem)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(dataDirectoryFileSystem);
        for (var index = 0; index < contentStore.Buckets.Count; index++)
        {
            var bucket = contentStore.Buckets[index];
            var segments = bucket.ContentPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
            var share = segments.Length > 0 ? contentStore.Shares.FirstOrDefault(s => s.Name == segments[0]) : null;
            var isUsable = share is not null ? IsFolder(share.FileSystem, share.Path, segments[1..])
                : commandLine.DataDirectory is { } dataDirectory ? IsFolder(dataDirectoryFileSystem, Path.GetFullPath(dataDirectory), segments)
                : contentStore.SeedFolder(bucket.ContentPath) != ContentChangeResult.NotPermitted;
            if (!isUsable)
            {
                return BucketDirectoryCannotBeOpened(commandLine.Buckets[index].Path, bucket.Name);
            }
        }

        return null;
    }

    private static bool IsFolder(IContentFileSystem fileSystem, string root, string[] segments) =>
        fileSystem.GetEntryKind(Path.Combine([root, .. segments])) == ContentEntryKind.Directory;

    private static string AccessWord(ContentShareAccess access) => access == ContentShareAccess.ReadWrite ? "rw" : "r";

    // Each --share-access row's members expanded to account names, @everyone kept as its marker; rows naming
    // one share merge, and a member reached twice keeps the wider access.
    private static string? ComposeGrants(
        IReadOnlyList<ConfiguredShareAccess> rows,
        AccountGroupExpansion groups,
        Dictionary<string, Dictionary<string, ContentShareAccess>> grantsByShare)
    {
        foreach (var row in rows)
        {
            if (!grantsByShare.TryGetValue(row.Share, out var grants))
            {
                grants = new Dictionary<string, ContentShareAccess>(StringComparer.Ordinal);
                grantsByShare.Add(row.Share, grants);
            }

            foreach (var grant in row.Grants)
            {
                var access = grant.Access == "rw" ? ContentShareAccess.ReadWrite : ContentShareAccess.Read;
                if (ReachedBy(grant.Member, groups, out var reached) is { } failure)
                {
                    return failure;
                }

                foreach (var member in reached)
                {
                    grants[member] = grants.TryGetValue(member, out var earlier) && earlier > access ? earlier : access;
                }
            }
        }

        return null;
    }

    private static string? ReachedBy(ConfiguredMember member, AccountGroupExpansion groups, out IReadOnlySet<string> reached)
    {
        if (member.IsGroup && member.Name == AccountGroupExpansion.Everyone)
        {
            reached = new HashSet<string>(StringComparer.Ordinal) { ContentShare.EveryoneMember };
            return null;
        }

        return groups.Expand(ShareAccessOption, [member], out reached);
    }

    private static (ContentShare? Share, SurlExitCode ExitCode, string? FailureMessage) ComposeShare(
        ConfiguredShare configured,
        string? dataDirectory,
        List<ContentShare> earlier,
        Dictionary<string, Dictionary<string, ContentShareAccess>> grantsByShare,
        Func<string, bool> canOpenShareDirectory,
        IContentFileSystem shareFileSystem)
    {
        var fullPath = Path.GetFullPath(configured.Path);
        ContentShare share;
        try
        {
            share = new ContentShare(configured.Name, fullPath, shareFileSystem)
            {
                IsReadOnly = configured.IsReadOnly,
                Schemes = WithTlsTwins(configured.Protocols),
                Grants = grantsByShare.GetValueOrDefault(configured.Name),
            };
        }
        catch (ArgumentException)
        {
            return (null, SurlExitCode.FailedInit, ShareNameIsNotAFolderName(configured.Name));
        }

        if (!canOpenShareDirectory(fullPath))
        {
            return (null, SurlExitCode.CouldNotReadFile, ShareDirectoryCannotBeOpened(configured.Path, configured.Name));
        }

        if (dataDirectory is not null && Overlap(fullPath, Path.GetFullPath(dataDirectory)))
        {
            return (null, SurlExitCode.FailedInit, ShareOverlaps(configured.Name, "--directory"));
        }

        var overlapped = earlier.FirstOrDefault(other => Overlap(fullPath, other.Path));
        return overlapped is null
            ? (share, SurlExitCode.Ok, null)
            : (null, SurlExitCode.FailedInit, ShareOverlaps(configured.Name, overlapped.Name));
    }

    // The record holds the path percent-decoded; the store reads it as a request path, so each segment is
    // encoded again.
    private static (ContentBucket? Bucket, string? FailureMessage) ComposeBucket(ConfiguredBucket configured)
    {
        try
        {
            return (new ContentBucket(configured.Name, string.Join('/', configured.Path.Split('/').Select(Uri.EscapeDataString))), null);
        }
        catch (ArgumentException refusal)
        {
            return (null, refusal.ParamName == "name"
                ? BucketNameIsNotAFolderName(configured.Name)
                : BucketPathIsNotAContentPath(configured.Name, configured.Path));
        }
    }

    // Two full paths overlap when one equals or lies inside the other, compared ignoring case so a
    // case-insensitive file system cannot reach one file by two names.
    private static bool Overlap(string path, string other) => IsInsideOrAt(path, other) || IsInsideOrAt(other, path);

    private static bool IsInsideOrAt(string path, string root)
    {
        var trimmedRoot = Path.TrimEndingDirectorySeparator(root);
        var trimmedPath = Path.TrimEndingDirectorySeparator(path);
        var prefix = Path.EndsInDirectorySeparator(trimmedRoot) ? trimmedRoot : trimmedRoot + Path.DirectorySeparatorChar;
        return string.Equals(trimmedPath, trimmedRoot, StringComparison.OrdinalIgnoreCase)
            || trimmedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
