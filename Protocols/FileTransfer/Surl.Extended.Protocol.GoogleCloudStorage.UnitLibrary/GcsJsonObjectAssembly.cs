using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Writes one new generation of an object from the bytes of one or more existing objects, which is what copy, rewrite and
/// compose all do (ADR-0148, decision 7): the destination's name and conditions, the size against <c>--max-filesize</c>, its
/// folders, the bytes through an upload session committed only once they are all in, and the metadata kept in the index.
/// </summary>
/// <param name="store">The content store read from and written to.</param>
/// <param name="properties">Reads the object at the destination now, and the one just written.</param>
/// <param name="putObject">Creates folders, measures the limit and keeps the metadata as PUT Object does.</param>
internal sealed class GcsJsonObjectAssembly(ContentStore store, GcsObjectPropertiesReader properties, GcsPutObject putObject)
{
    /// <summary>
    /// A source object: where its file is, and what it is now.
    /// </summary>
    /// <param name="Mapping">Where the file is.</param>
    /// <param name="Found">The object as it is now.</param>
    internal sealed record SourceObject(ContentPathMapping Mapping, ObjectProperties Found);

    /// <summary>
    /// Where the new generation goes and what it is stored with.
    /// </summary>
    /// <param name="RawBucket">The bucket as sent.</param>
    /// <param name="RawName">The object's name as sent.</param>
    /// <param name="Metadata">The metadata stored with it.</param>
    /// <param name="Conditions">The conditions the object there now must meet.</param>
    internal sealed record Destination(string RawBucket, string RawName, ObjectMetadata Metadata, GcsJsonConditions Conditions);

    /// <summary>
    /// Writes the destination as the sources' bytes, one after the other.
    /// </summary>
    /// <param name="destination">Where, and with what metadata.</param>
    /// <param name="sources">The sources, in order.</param>
    /// <param name="caller">Who writes, on <c>gs</c>.</param>
    /// <param name="fields">The request's fields, for the HTTP <c>If-*</c> conditions.</param>
    /// <param name="now">The upload time, which becomes the generation.</param>
    /// <param name="cancellationToken">Cancels reading and writing.</param>
    /// <returns>The refusal; or, none, the object written and its digests.</returns>
    public async Task<(GcsJsonError? Refusal, ObjectProperties? Stored, GcsObjectDigests? Digests)> WriteAsync(Destination destination, IReadOnlyList<SourceObject> sources, ContentCaller caller, IReadOnlyList<KeyValuePair<string, string>> fields, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (refusal, mapping) = await PrepareAsync(destination, sources, caller, fields, cancellationToken);
        if (refusal is not null)
        {
            return (refusal, null, null);
        }

        var opening = await store.OpenUploadAsync(mapping!, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);
        if (opening.Session is not { } session)
        {
            return (opening.Result == ContentUploadOpeningResult.IsADirectory ? GcsJsonError.Conflict : GcsJsonError.Forbidden, null, null);
        }

        await using (session)
        {
            return await CopyAndCommitAsync(destination, sources, mapping!, session, now, cancellationToken);
        }
    }

    // The name, the limit, the conditions and the folders: everything decided before a byte moves.
    private async Task<(GcsJsonError? Refusal, ContentPathMapping? Mapping)> PrepareAsync(Destination destination, IReadOnlyList<SourceObject> sources, ContentCaller caller, IReadOnlyList<KeyValuePair<string, string>> fields, CancellationToken cancellationToken)
    {
        if (destination.RawName.EndsWith('/') || GcsObjectName.IsTooLong(destination.RawName)
            || GcsPutObject.UserMetadataBytes(destination.Metadata.UserMetadata) > GcsPutObject.MaxUserMetadataBytes)
        {
            return (GcsJsonError.Invalid, null);
        }

        if (putObject.IsPastTheUploadLimit(sources.Sum(source => source.Found.Length)))
        {
            return (GcsJsonError.UploadTooLarge, null);
        }

        var mapping = store.MapBucketRequestPathForCaller($"/{destination.RawBucket}/{destination.RawName}", caller);
        if (!mapping.IsMapped)
        {
            return (GcsJsonError.Invalid, null);
        }

        var current = await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, destination.RawBucket, destination.RawName), cancellationToken);
        if (!destination.Conditions.AreMetByWrite(fields, current))
        {
            return (GcsJsonError.ConditionNotMet, null);
        }

        return putObject.CreateFolders(destination.RawBucket, destination.RawName, caller) is { } folderRefusal
            ? (folderRefusal == GcsError.ConflictingObjectName ? GcsJsonError.Conflict : GcsJsonError.Forbidden, null)
            : (null, mapping);
    }

    // Each source's bytes through the hashes into the session (the total was checked against --max-filesize before any byte moved); committed, then kept in the index and read back.
    private async Task<(GcsJsonError? Refusal, ObjectProperties? Stored, GcsObjectDigests? Digests)> CopyAndCommitAsync(Destination destination, IReadOnlyList<SourceObject> sources, ContentPathMapping mapping, ContentUploadSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var hashes = new GcsObjectHashes();
        await using var sink = new GcsHashingStream(hashes, session);
        foreach (var source in sources)
        {
            await store.CopyFileBytesAsync(source.Mapping, ContentByteRange.WholeFile(source.Found.Length), sink, cancellationToken);
        }

        var digests = hashes.Finish();
        if (await session.CommitAsync(cancellationToken) != ContentUploadResult.Written)
        {
            return (GcsJsonError.Forbidden, null, null);
        }

        var (refusal, _) = await putObject.StoreMetadataAsync(destination.RawBucket, destination.RawName, mapping, destination.Metadata, digests.ETag, digests.Crc32cValue, now, cancellationToken);
        if (refusal is not null)
        {
            return (refusal == GcsError.MetadataTooLarge ? GcsJsonError.Invalid : GcsJsonError.QuotaExceeded, null, null);
        }

        var stored = await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, destination.RawBucket, destination.RawName), cancellationToken);

        return (null, stored, digests);
    }
}
