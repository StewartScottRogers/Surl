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
    private IReadOnlyList<OciDescriptor>? index;

    private OciRepository(ContentStore store, ContentCaller caller, string name, IExchangeLog log)
    {
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
    /// <returns>The repository, or <see langword="null"/> when its folder holds no <c>oci-layout</c> the caller may read.</returns>
    public static OciRepository? Open(ContentStore store, ContentCaller caller, string name, IExchangeLog log)
    {
        var layout = store.MapRequestPathForCaller($"/{name}/oci-layout", caller);

        return layout.IsMapped && store.GetEntryKind(layout) == ContentEntryKind.File
            ? new OciRepository(store, caller, name, log)
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

        return status is not null && await HashMatchesAsync(mapping, status.Length, digest, cancellationToken)
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
        using var hash = digest.CreateHash();
        hash.AppendData(bytes);

        return Verified(digest, hash.GetHashAndReset()) ? bytes : null;
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
