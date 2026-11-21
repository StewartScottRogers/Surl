using Surl.Composition;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s content store, as a test seeds it (ADR-0124, decision 3): the one store
/// <c>http</c>, <c>ftp</c>, <c>sftp</c> and the other file servers read and write. Thread-safe.
/// </summary>
public sealed class SurlServerFiles
{
    private readonly ComposedStores stores;

    private readonly Action throwIfStopped;

    internal SurlServerFiles(ComposedStores stores, Action throwIfStopped)
    {
        this.stores = stores;
        this.throwIfStopped = throwIfStopped;
    }

    /// <summary>
    /// Writes <paramref name="contents"/> at <paramref name="path"/> as an upload would, making any folder on the
    /// way and replacing a file there; the fault plan is not consulted. Publishes a
    /// <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Seeded"/>.
    /// </summary>
    /// <param name="path">Where the file goes, as a request path such as <c>/reports/a.txt</c>.</param>
    /// <param name="contents">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the file is in the store.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is one a request may not name, such as a relative path or one with a <c>..</c> segment.</exception>
    /// <exception cref="SurlSeedRefusedException">No client may write there (<c>NotPermitted</c>, such as a
    /// <c>.surl</c> path) or the file passes the size limit (<c>TooLarge</c>).</exception>
    public async Task PutAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = new MemoryStream(contents.ToArray(), writable: false);
        var result = await stores.SeedFileAsync(path, stream, cancellationToken);
        if (result != ContentUploadResult.Written)
        {
            throw new SurlSeedRefusedException(StoreKind.Content, result.ToString(), $"The file {path}");
        }
    }

    /// <summary>
    /// Deletes the file at <paramref name="path"/> as the test's own delete: no client permission is needed and the
    /// fault plan is not consulted. Publishes a <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Deleted"/>.
    /// </summary>
    /// <remarks>
    /// Only the file goes. The folders on its path stay, empty or not, as a client's delete leaves them (ADR-0161
    /// decision 1), although <c>PutAsync</c> makes any folder that is missing; so deleting a folder's only file leaves
    /// the empty folder in a listing. <see cref="SurlServer.ResetAsync"/> (or restoring a snapshot) returns the
    /// content store to its configured state.
    /// </remarks>
    /// <param name="path">The file's request path, such as <c>/reports/a.txt</c>.</param>
    /// <returns>A task that completes once the file is gone.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is one a request may not name, such as a relative path or one with a <c>..</c> segment.</exception>
    /// <exception cref="SurlSeedRefusedException">No file is there (<c>Absent</c>: nothing, a folder, or a hidden
    /// path).</exception>
    public async Task DeleteAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var result = await stores.DeleteFileAsync(path);
        if (result != ContentChangeResult.Done)
        {
            throw new SurlSeedRefusedException(StoreKind.Content, result.ToString(), $"Deleting the file {path}");
        }
    }

    /// <summary>
    /// Reads the file at <paramref name="path"/>: its exact bytes, size, modification time and content type.
    /// Publishes nothing, asks no fault rule, and works on a <c>DataDirectory</c> server (ADR-0164, decisions 1 to 4).
    /// </summary>
    /// <param name="path">The file's request path, such as <c>/reports/a.txt</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="SurlNotFoundException">No file is there: nothing, a folder, or a hidden path such as one under <c>.surl</c>.</exception>
    /// <exception cref="SurlReadRefusedException">The file is past 16 MiB, or the path is one a request may not name.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public async Task<SurlFileContents> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        throwIfStopped();
        var file = SurlReadResults.ValueOrThrow(await stores.ReadFileAsync(path, cancellationToken));
        return new SurlFileContents(file.Bytes, file.ByteCount, file.ModifiedAt, file.ContentType);
    }

    /// <summary>
    /// Lists the children of the folder at <paramref name="path"/> in ordinal order of name, one page at a time.
    /// Publishes nothing and asks no fault rule (ADR-0164, decisions 1 to 4).
    /// </summary>
    /// <param name="path">The folder's request path; <c>/</c> for the root.</param>
    /// <param name="afterName">The last name seen, or null to start at the first.</param>
    /// <param name="limit">The most children to return, 1 to 1000.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="SurlNotFoundException">No folder is there.</exception>
    /// <exception cref="SurlReadRefusedException">The limit is outside 1 to 1000, or the path is one a request may not name.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<SurlFolderPage> ListAsync(string path, string? afterName = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        throwIfStopped();
        return SurlReadResults.Completed(() =>
        {
            var page = SurlReadResults.ValueOrThrow(stores.ListFolder(path, afterName, limit, cancellationToken));
            return new SurlFolderPage([.. page.Entries.Select(entry => new SurlFolderEntry(entry.Name, entry.IsFolder, entry.ByteCount))], page.HasMore);
        });
    }
}
