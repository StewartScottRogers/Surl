using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Opens the repository a push names, creating it when it is none yet (ADR-0115, decision 2.2):
/// its folder and any missing parent folders, its <c>oci-layout</c> and its <c>blobs</c> folder.
/// </summary>
/// <remarks>
/// Refused with <c>400 NAME_INVALID</c>: a segment the store refuses (a reserved device name), a
/// segment that is a file, a name inside another repository's <c>blobs</c> folder, and a folder
/// that exists, is not empty and holds no <c>oci-layout</c>. A first segment naming a share the
/// caller may not see is <c>404 NAME_UNKNOWN</c>, as for a read; a folder or file the store does
/// not permit the caller to write is <c>403 DENIED</c>.
/// </remarks>
internal static class OciRepositoryCreation
{
    private static readonly byte[] LayoutFile = """{"imageLayoutVersion":"1.0.0"}"""u8.ToArray();

    /// <summary>
    /// Opens or creates the repository <paramref name="name"/> for <paramref name="caller"/>.
    /// </summary>
    /// <param name="store">The shared content store.</param>
    /// <param name="caller">Who pushes.</param>
    /// <param name="name">A name that matches Dist's grammar.</param>
    /// <param name="log">Where the repository notes a file that fails its check.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The repository, or the error that refuses it.</returns>
    public static async Task<(OciRepository? Repository, OciError? Error)> OpenOrCreateAsync(
        ContentStore store,
        ContentCaller caller,
        string name,
        IExchangeLog log,
        CancellationToken cancellationToken)
    {
        if (OciRepository.Open(store, caller, name, log) is { } existing)
        {
            return (existing, null);
        }

        var segments = name.Split('/');
        for (var count = 1; count <= segments.Length; count++)
        {
            if (CreateFolder(store, caller, segments, count, cancellationToken) is { } refusal)
            {
                return (null, refusal);
            }
        }

        using var layout = new MemoryStream(LayoutFile);
        if (await store.WriteUploadAsync(store.MapRequestPathForCaller($"/{name}/oci-layout", caller), layout, cancellationToken) != ContentUploadResult.Written)
        {
            return (null, OciError.Denied);
        }

        store.CreateDirectory(store.MapRequestPathForCaller($"/{name}/blobs", caller));

        return (OciRepository.Open(store, caller, name, log), null);
    }

    // The folder of the first count segments: kept when it may hold the repository, created when
    // missing, else the refusal.
    private static OciError? CreateFolder(ContentStore store, ContentCaller caller, string[] segments, int count, CancellationToken cancellationToken)
    {
        var path = "/" + string.Join('/', segments[..count]);
        var folder = store.MapRequestPathForCaller(path, caller);
        if (!folder.IsMapped || folder.EntryKind == ContentEntryKind.File)
        {
            return OciError.NameInvalid;
        }

        if (folder.EntryKind == ContentEntryKind.Directory)
        {
            return ExistingFolderRefusal(store, caller, path, folder, count == segments.Length ? null : segments[count], cancellationToken);
        }

        if (count == 1 && store.Shares.Any(share => share.Name == segments[0]))
        {
            return OciError.NameUnknown;
        }

        return store.CreateDirectory(folder) == ContentChangeResult.Done ? null : OciError.Denied;
    }

    // An existing folder on the way refuses a name inside its blobs folder when it is a
    // repository; the repository's own folder refuses when it holds other files.
    private static OciError? ExistingFolderRefusal(ContentStore store, ContentCaller caller, string path, ContentPathMapping folder, string? nextSegment, CancellationToken cancellationToken)
    {
        if (nextSegment is null)
        {
            return store.ListDirectoryWhateverTheListingSwitchSays(folder, cancellationToken).Entries.Count == 0 ? null : OciError.NameInvalid;
        }

        var isRepository = store.GetEntryKind(store.MapRequestPathForCaller($"{path}/oci-layout", caller)) == ContentEntryKind.File;

        return isRepository && nextSegment == "blobs" ? OciError.NameInvalid : null;
    }
}
