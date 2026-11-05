using Surl.Content;

namespace Surl.Api;

/// <summary>
/// The files <see cref="SurlServerOptions.Files"/> supplies, checked and written into the
/// in-memory file system served without a data directory (ADR-0099, decision 4).
/// </summary>
internal static class InMemoryFiles
{
    /// <summary>
    /// Throws when a path is not a plain relative path, a file's bytes are null, or a path names a
    /// directory another path is inside.
    /// </summary>
    /// <param name="files">The files, by relative path.</param>
    /// <exception cref="ArgumentException">A path or its bytes are misused.</exception>
    public static void ThrowIfMisused(IDictionary<string, byte[]> files)
    {
        foreach (var (path, contents) in files)
        {
            if (!IsPlainRelativePath(path) || contents is null)
            {
                throw new ArgumentException(
                    $"Files holds '{path}', which is not a '/' separated relative path with bytes.", "options");
            }

            if (files.Keys.Any(other => other.StartsWith(path + "/", StringComparison.Ordinal)))
            {
                throw new ArgumentException($"Files holds '{path}' and a file inside it.", "options");
            }
        }
    }

    /// <summary>Writes <paramref name="files"/> into a new in-memory file system, creating each file's directories.</summary>
    /// <param name="files">The files, by relative path, already checked.</param>
    /// <param name="timeProvider">The clock the file system's last-write times come from.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>The file system, its root <see cref="InMemoryContentFileSystem.RootPath"/>.</returns>
    public static async Task<InMemoryContentFileSystem> ComposeAsync(
        IDictionary<string, byte[]> files, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var fileSystem = new InMemoryContentFileSystem(timeProvider);
        foreach (var (path, contents) in files)
        {
            var fullPath = Path.Combine([InMemoryContentFileSystem.RootPath, .. path.Split('/')]);
            fileSystem.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using var stream = fileSystem.CreateFileForAsyncWrite(fullPath);
            await stream.WriteAsync(contents, cancellationToken);
        }

        return fileSystem;
    }

    private static bool IsPlainRelativePath(string path) =>
        path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not ".." && segment.IndexOfAny(['\\', ':']) < 0);
}
