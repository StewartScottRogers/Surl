using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// One repository: a folder of the shared content store holding an OCI Image Layout - an
/// <c>oci-layout</c> file, <c>blobs/&lt;algorithm&gt;/&lt;hex&gt;</c> and <c>index.json</c>
/// (Image "OCI Image Layout Specification"; ADR-0115, decision 2.1) - read as one caller.
/// </summary>
/// <remarks>
/// A digest names a manifest exactly when <c>index.json</c> lists it, or an image index it lists
/// names it among its <c>manifests</c>; every other file under <c>blobs/</c> is a blob. A file is
/// served only once its bytes hash to the digest its name claims; one that does not is answered as
/// absent and noted.
/// </remarks>
internal sealed class OciRepository
{
    /// <summary>
    /// The largest manifest read, 4 MiB (ADR-0115, decision 7).
    /// </summary>
    public const long MaxManifestBytes = 4 * 1024 * 1024;

    /// <summary>
    /// The largest <c>index.json</c> read, 16 MiB: 10000 descriptors with their annotations fit
    /// well inside it (ADR-0115, decision 7). A larger one is read as an empty index.
    /// </summary>
    public const long MaxIndexBytes = 16 * 1024 * 1024;

    private const int HashChunkBytes = 1024 * 1024;

    private readonly ContentStore store;
    private readonly ContentCaller caller;
    private readonly IExchangeLog log;
    private readonly ObjectMetadataIndex? metadataIndex;
    private IReadOnlyList<OciDescriptor>? index;

    private OciRepository(ContentStore store, ContentCaller caller, string name, IExchangeLog log, ObjectMetadataIndex? metadataIndex)
    {
        this.metadataIndex = metadataIndex;
        this.store = store;
        this.caller = caller;
        this.log = log;
        Name = name;
    }

    /// <summary>
    /// The repository's name, which is its folder's path in the content store.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Opens the repository <paramref name="name"/> as <paramref name="caller"/>.
    /// </summary>
    /// <param name="store">The shared content store.</param>
    /// <param name="caller">Who asks; a share the caller may not read holds no repository.</param>
    /// <param name="name">A name that matches Dist's grammar.</param>
    /// <param name="log">Where a file that fails its check is noted.</param>
    /// <param name="metadataIndex">
    /// Where each file's verified digest is cached against its stamp, so a file is hashed once
    /// until it changes; <see langword="null"/> hashes on every read.
    /// </param>
    /// <returns>The repository, or <see langword="null"/> when its folder holds no <c>oci-layout</c> the caller may read.</returns>
    public static OciRepository? Open(ContentStore store, ContentCaller caller, string name, IExchangeLog log, ObjectMetadataIndex? metadataIndex)
    {
        var layout = store.MapRequestPathForCaller($"/{name}/oci-layout", caller);

        return layout.IsMapped && store.GetEntryKind(layout) == ContentEntryKind.File
            ? new OciRepository(store, caller, name, log, metadataIndex)
            : null;
    }

    /// <summary>
    /// The descriptors <c>index.json</c> lists, read once. A missing index is empty; one that does
    /// not parse, or is past <see cref="MaxIndexBytes"/>, is empty and noted (ADR-0115, decision 2.1).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The descriptors, in the order written.</returns>
    public async Task<IReadOnlyList<OciDescriptor>> ReadIndexAsync(CancellationToken cancellationToken) =>
        index ??= await LoadIndexAsync(cancellationToken);

    /// <summary>
    /// Finds the manifest a tag or digest names, with its verified bytes.
    /// </summary>
    /// <param name="reference">A tag or a digest.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The manifest, or <see langword="null"/> for <c>MANIFEST_UNKNOWN</c>.</returns>
    public async Task<OciFoundManifest?> FindManifestAsync(string reference, CancellationToken cancellationToken)
    {
        var descriptor = reference.Contains(':')
            ? await FindManifestByDigestAsync(reference, cancellationToken)
            : await FindTaggedAsync(reference, cancellationToken);

        return descriptor is null ? null : await ReadManifestAsync(descriptor, cancellationToken);
    }

    /// <summary>
    /// Reads the manifest a descriptor names, checked against its digest.
    /// </summary>
    /// <param name="descriptor">A descriptor of this repository's index.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The manifest, or <see langword="null"/> when its file is missing, too large or does not match.</returns>
    public async Task<OciFoundManifest?> ReadManifestAsync(OciDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (OciDigest.Parse(descriptor.Digest) is not { } digest)
        {
            return null;
        }

        var bytes = await ReadVerifiedAsync(digest, MaxManifestBytes, cancellationToken);

        return bytes is null ? null : new OciFoundManifest(descriptor, digest, bytes);
    }

    /// <summary>
    /// Finds the blob a digest names, its bytes checked against it.
    /// </summary>
    /// <param name="digest">The digest.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The blob's mapping and length, or <see langword="null"/> for <c>BLOB_UNKNOWN</c>.</returns>
    public async Task<OciFoundBlob?> FindBlobAsync(OciDigest digest, CancellationToken cancellationToken)
    {
        if (await FindManifestByDigestAsync(digest.ToString(), cancellationToken) is not null)
        {
            return null;
        }

        var mapping = store.MapRequestPathForCaller(digest.BlobPathIn(Name), caller);
        var status = store.GetFileStatus(mapping);

        return status is not null && await IsVerifiedAsync(digest, status, () => HashMatchesAsync(mapping, status.Length, digest, cancellationToken), cancellationToken)
            ? new OciFoundBlob(mapping, status.Length)
            : null;
    }

    /// <summary>
    /// Every manifest the repository holds: each descriptor <c>index.json</c> lists, then each one
    /// a listed image index names, each digest once.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The descriptors.</returns>
    public async Task<IReadOnlyList<OciDescriptor>> ReadManifestDescriptorsAsync(CancellationToken cancellationToken)
    {
        var listed = await ReadIndexAsync(cancellationToken);
        var all = new List<OciDescriptor>(listed);
        foreach (var descriptor in listed.Where(descriptor => descriptor.IsImageIndex))
        {
            if (await ReadManifestAsync(descriptor, cancellationToken) is { } child && OciManifestJson.Read(child.Bytes) is { } facts)
            {
                all.AddRange(facts.Manifests);
            }
        }

        return all.DistinctBy(descriptor => descriptor.Digest, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Reads <c>index.json</c> again, past what an earlier read cached, for a change written
    /// under the server's index lock.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The descriptors as stored now.</returns>
    public Task<IReadOnlyList<OciDescriptor>> RereadIndexAsync(CancellationToken cancellationToken)
    {
        index = null;

        return ReadIndexAsync(cancellationToken);
    }

    /// <summary>
    /// Rewrites <c>index.json</c> whole, through the content store's temporary file and rename, so
    /// a reader sees the old or the new index, never half of one (ADR-0115, decision 2.1).
    /// </summary>
    /// <param name="descriptors">The descriptors to list.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="false"/> when the store does not permit the write.</returns>
    public async Task<bool> WriteIndexAsync(IReadOnlyList<OciDescriptor> descriptors, CancellationToken cancellationToken)
    {
        index = descriptors;
        using var bytes = new MemoryStream(OciIndexFile.Render(descriptors));

        return await store.WriteUploadAsync(Map("index.json"), bytes, cancellationToken) == ContentUploadResult.Written;
    }

    /// <summary>
    /// Writes a manifest's bytes as the blob file of its digest.
    /// </summary>
    /// <param name="digest">The manifest's digest.</param>
    /// <param name="bytes">Its bytes, exactly as pushed.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="false"/> when the store does not permit the write.</returns>
    public async Task<bool> WriteManifestFileAsync(OciDigest digest, byte[] bytes, CancellationToken cancellationToken)
    {
        CreateAlgorithmFolder(digest);
        using var source = new MemoryStream(bytes);

        return await store.WriteUploadAsync(store.MapRequestPathForCaller(digest.BlobPathIn(Name), caller), source, cancellationToken) == ContentUploadResult.Written;
    }

    /// <summary>
    /// Opens an upload session staged in the repository's <c>blobs</c> folder: the content store's
    /// upload session keeps the bytes in a hidden temporary file there until they are complete.
    /// </summary>
    /// <param name="id">The session's UUID.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    /// <returns>The session, or <see langword="null"/> when the store does not permit the upload.</returns>
    public async Task<OciUploadSession?> OpenUploadSessionAsync(string id, CancellationToken cancellationToken)
    {
        store.CreateDirectory(Map("blobs"));
        var stagingPath = $"/{Name}/blobs/oci-upload-{id}";
        var opening = await store.OpenUploadAsync(
            store.MapRequestPathForCaller(stagingPath, caller),
            new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false),
            cancellationToken);

        return opening.Session is { } staging ? new OciUploadSession(id, Name, stagingPath, staging) : null;
    }

    /// <summary>
    /// Moves a complete upload's bytes to the blob file of <paramref name="digest"/>: committed to
    /// the session's staging file, then renamed over <c>blobs/&lt;algorithm&gt;/&lt;hex&gt;</c>.
    /// </summary>
    /// <param name="session">The session, whose bytes hash to <paramref name="digest"/>.</param>
    /// <param name="digest">The blob's digest.</param>
    /// <param name="cancellationToken">Cancels the commit.</param>
    /// <returns><see langword="false"/> when the store does not permit the commit or the rename.</returns>
    public async Task<bool> StoreBlobAsync(OciUploadSession session, OciDigest digest, CancellationToken cancellationToken)
    {
        CreateAlgorithmFolder(digest);

        // Never TooLarge here: an upload past the limit was answered 413 before it could complete.
        _ = await session.Staging.CommitAsync(cancellationToken);
        var staged = store.MapRequestPathForCaller(session.StagingPath, caller);

        return store.RenameEntry(staged, store.MapRequestPathForCaller(digest.BlobPathIn(Name), caller)) == ContentChangeResult.Done;
    }

    /// <summary>
    /// Copies a blob's bytes to <paramref name="destination"/>.
    /// </summary>
    /// <param name="blob">A blob of this repository.</param>
    /// <param name="destination">Where the bytes go.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A task that completes once every byte is copied.</returns>
    public async Task CopyBlobAsync(OciFoundBlob blob, Stream destination, CancellationToken cancellationToken)
    {
        if (blob.Length > 0)
        {
            await store.CopyFileBytesAsync(blob.Mapping, ContentByteRange.WholeFile(blob.Length), destination, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes the blob file of <paramref name="digest"/>.
    /// </summary>
    /// <param name="digest">The digest.</param>
    /// <returns>What the store did.</returns>
    public ContentChangeResult DeleteBlobFile(OciDigest digest) =>
        store.DeleteFile(store.MapRequestPathForCaller(digest.BlobPathIn(Name), caller));

    private void CreateAlgorithmFolder(OciDigest digest)
    {
        store.CreateDirectory(Map("blobs"));
        store.CreateDirectory(Map($"blobs/{digest.Algorithm}"));
    }

    private ContentPathMapping Map(string relativePath) => store.MapRequestPathForCaller($"/{Name}/{relativePath}", caller);

    private async Task<OciDescriptor?> FindTaggedAsync(string tag, CancellationToken cancellationToken) =>
        OciNames.IsTag(tag)
            ? (await ReadIndexAsync(cancellationToken)).FirstOrDefault(descriptor => descriptor.Tag == tag)
            : null;

    private async Task<OciDescriptor?> FindManifestByDigestAsync(string digest, CancellationToken cancellationToken) =>
        OciDigest.Parse(digest) is null
            ? null
            : (await ReadManifestDescriptorsAsync(cancellationToken)).FirstOrDefault(descriptor => descriptor.Digest == digest);

    private async Task<IReadOnlyList<OciDescriptor>> LoadIndexAsync(CancellationToken cancellationToken)
    {
        var mapping = store.MapRequestPathForCaller($"/{Name}/index.json", caller);
        if (store.GetFileStatus(mapping) is not { } status)
        {
            return [];
        }

        var facts = status.Length <= MaxIndexBytes ? OciManifestJson.Read(await ReadAllAsync(mapping, status.Length, cancellationToken)) : null;
        if (facts is null)
        {
            log.Note($"The index.json of repository {Name} is not an image index; it is read as empty.");
        }

        return facts?.Manifests ?? [];
    }

    // A blob file's bytes, when it exists, is at most maxBytes long and hashes to its digest.
    private async Task<byte[]?> ReadVerifiedAsync(OciDigest digest, long maxBytes, CancellationToken cancellationToken)
    {
        var mapping = store.MapRequestPathForCaller(digest.BlobPathIn(Name), caller);
        if (store.GetFileStatus(mapping) is not { } status || status.Length > maxBytes)
        {
            return null;
        }

        var bytes = await ReadAllAsync(mapping, status.Length, cancellationToken);

        return await IsVerifiedAsync(digest, status, () => Task.FromResult(HashMatches(bytes, digest)), cancellationToken) ? bytes : null;
    }

    private bool HashMatches(byte[] bytes, OciDigest digest)
    {
        using var hash = digest.CreateHash();
        hash.AppendData(bytes);

        return Verified(digest, hash.GetHashAndReset());
    }

    // Whether the blob file of digest, whose status was read before its bytes, hashes to the
    // digest: answered from the metadata index while the file's stamp is the one recorded, else by
    // check, whose success is recorded against that status (ADR-0115, decision 2.1).
    private async Task<bool> IsVerifiedAsync(OciDigest digest, ContentFileStatus status, Func<Task<bool>> check, CancellationToken cancellationToken)
    {
        var key = IndexKeyOf(digest);
        if (key is not null && await metadataIndex!.IsDigestVerifiedAsync(key, digest.ToString(), cancellationToken))
        {
            return true;
        }

        if (!await check())
        {
            return false;
        }

        if (key is not null)
        {
            await metadataIndex!.RecordVerifiedDigestAsync(key, digest.ToString(), status, cancellationToken);
        }

        return true;
    }

    // The metadata index key of the blob file of digest - its path relative to the served root -
    // or null when there is no index, or the repository is in a share, outside the root it keys.
    private string? IndexKeyOf(OciDigest digest)
    {
        var key = digest.BlobPathIn(Name)[1..];
        var firstSegment = key[..key.IndexOf('/')];

        return metadataIndex is null || store.Shares.Any(share => share.Name == firstSegment) ? null : key;
    }

    private async Task<bool> HashMatchesAsync(ContentPathMapping mapping, long length, OciDigest digest, CancellationToken cancellationToken)
    {
        using var hash = digest.CreateHash();
        using var chunk = new MemoryStream();
        for (long offset = 0; offset < length; offset += HashChunkBytes)
        {
            chunk.SetLength(0);
            await store.CopyFileBytesAsync(mapping, ContentByteRange.Select(length, offset, offset + HashChunkBytes - 1), chunk, cancellationToken);
            hash.AppendData(chunk.GetBuffer(), 0, (int)chunk.Length);
        }

        return Verified(digest, hash.GetHashAndReset());
    }

    private bool Verified(OciDigest digest, byte[] hash)
    {
        if (digest.IsHashOf(hash))
        {
            return true;
        }

        log.Note($"The blob file of {digest} in repository {Name} does not hash to its digest; it is answered as absent.");

        return false;
    }

    private async Task<byte[]> ReadAllAsync(ContentPathMapping mapping, long length, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        if (length > 0)
        {
            await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(length), buffer, cancellationToken);
        }

        return buffer.ToArray();
    }
}
