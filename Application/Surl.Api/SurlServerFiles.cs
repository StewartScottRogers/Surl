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

    internal SurlServerFiles(ComposedStores stores) => this.stores = stores;

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
    /// <param name="path">The file's request path, such as <c>/reports/a.txt</c>.</param>
    /// <returns>A task that completes once the file is gone.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
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
}
