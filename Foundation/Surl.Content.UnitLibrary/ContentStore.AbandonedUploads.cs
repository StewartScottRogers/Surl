namespace Surl.Content;

/// <content>
/// The start-up sweep of upload temporary files a killed process left behind (ADR-0150).
/// </content>
public sealed partial class ContentStore
{
    private const string UploadTemporaryFilePrefix = ".surl-upload-";

    /// <summary>
    /// How long a <c>.surl-upload-</c> file must have gone unwritten before the sweep treats it as
    /// abandoned (ADR-0150 decision 2).
    /// </summary>
    public static readonly TimeSpan AbandonedUploadAge = TimeSpan.FromHours(24);

    /// <summary>
    /// Removes every upload temporary file - <c>.surl-upload-</c> and 32 hexadecimal digits - that
    /// was last written more than <see cref="AbandonedUploadAge"/> before now, in the served root
    /// and in each share that takes writes (ADR-0150).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Never removed: a younger file, a name of any other shape, a directory, anything in a
    /// <c>readonly</c> share, anything under the top-level <c>.surl</c> folder, anything reached
    /// through a symbolic link to a directory, and a file the file system refuses to delete (an
    /// open one on Windows). A file that cannot be examined or deleted is skipped, never thrown.
    /// </para>
    /// <para>
    /// The age is judged on <paramref name="timeProvider"/> against the file's last write time as
    /// the file system reports it.
    /// </para>
    /// </remarks>
    /// <param name="timeProvider">The clock that says now.</param>
    /// <returns>How many files were removed.</returns>
    public int RemoveAbandonedUploadFiles(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        DateTimeOffset cutoff = timeProvider.GetUtcNow() - AbandonedUploadAge;
        int removed = 0;
        foreach (ContentContainer container in new[] { rootContainer }.Concat(sharesByName.Values.Select(named => named.Container)))
        {
            if (!container.IsReadOnly)
            {
                removed += SweepDirectory(container.FileSystem, container.Root, container.ResolvedRoot, cutoff, isTop: true);
            }
        }

        return removed;
    }

    private static int SweepDirectory(IContentFileSystem fileSystem, string directory, string resolvedDirectory, DateTimeOffset cutoff, bool isTop)
    {
        string[] names = EntryNamesOrNone(fileSystem, directory);
        int removed = 0;
        foreach (string name in names)
        {
            if (isTop && string.Equals(name, ServiceStateFolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string path = Path.Join(directory, name);
            ContentEntryKind kind = fileSystem.GetEntryKind(path);
            if (kind == ContentEntryKind.Directory)
            {
                string resolved = fileSystem.ResolveFinalPath(path);
                if (string.Equals(resolved, Path.Join(resolvedDirectory, name), StringComparison.Ordinal))
                {
                    removed += SweepDirectory(fileSystem, path, resolved, cutoff, isTop: false);
                }
            }
            else if (kind == ContentEntryKind.File && IsUploadTemporaryFileName(name) && TryDeleteIfOlderThan(fileSystem, path, cutoff))
            {
                removed++;
            }
        }

        return removed;
    }

    private static string[] EntryNamesOrNone(IContentFileSystem fileSystem, string directory)
    {
        try
        {
            return [.. fileSystem.EnumerateDirectoryEntryNames(directory)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return [];
        }
    }

    private static bool IsUploadTemporaryFileName(string name) =>
        name.StartsWith(UploadTemporaryFilePrefix, StringComparison.Ordinal)
        && Guid.TryParseExact(name.AsSpan(UploadTemporaryFilePrefix.Length), "N", out _);

    private static bool TryDeleteIfOlderThan(IContentFileSystem fileSystem, string path, DateTimeOffset cutoff)
    {
        try
        {
            if (fileSystem.GetLastWriteTimeUtc(path) >= cutoff)
            {
                return false;
            }

            fileSystem.DeleteFile(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
