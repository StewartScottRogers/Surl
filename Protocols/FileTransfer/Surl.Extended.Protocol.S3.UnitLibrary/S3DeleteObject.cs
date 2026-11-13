using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers DeleteObject (DeleteObject; ADR-0108, decisions 1 and 3): removes the file at the key
/// and answers <c>204</c>, also for a key that does not exist.
/// </summary>
/// <remarks>
/// Deleting needs <c>--allow-uploads</c>, which the store's exposure options carry: off, every
/// DeleteObject is <c>403 AccessDenied</c> (ADR-0108, decision 4.1). A key ending in <c>/</c>
/// removes its folder only when the folder is empty, and forgets the folder object either way (the
/// index drops it, so ListObjectsV2 stops listing <c>x/</c> while the keys under it stay); a non-empty folder stays, and is still
/// <c>204</c>, as deleting a folder object leaves the keys under it. A key naming a folder without
/// the <c>/</c>, or nothing, or a path the store cannot hold, names no object: <c>204</c>. A
/// delete the store or a share refuses is <c>403 AccessDenied</c>.
/// </remarks>
/// <param name="store">The content store the object is removed from.</param>
/// <param name="index">The object metadata index a folder object is forgotten from.</param>
/// <param name="log">The exchange log an index write failure is noted in.</param>
internal sealed class S3DeleteObject(ContentStore store, ObjectMetadataIndex index, IExchangeLog log)
{
    private static readonly HttpStatus NoContent = new(204, "No Content");

    /// <summary>
    /// Answers a DeleteObject whose bucket exists.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The request's bucket and key.</param>
    /// <param name="caller">Who deletes, on <c>s3</c>.</param>
    /// <param name="cancellationToken">Cancels writing the index.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(S3Reply reply, S3RequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(S3Error.AccessDenied);
        }

        if (S3ObjectKey.IsTooLong(target.RawKey))
        {
            return reply.Error(S3Error.KeyTooLong);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);

        if (!mapping.IsMapped)
        {
            return reply.Answer(NoContent, [], HttpAnswerBody.None);
        }

        if (target.RawKey.EndsWith('/'))
        {
            return await DeleteFolderAsync(reply, target, mapping, cancellationToken);
        }

        return store.DeleteFile(mapping) == ContentChangeResult.NotPermitted
            ? reply.Error(S3Error.AccessDenied)
            : reply.Answer(NoContent, [], HttpAnswerBody.None);
    }

    // Removes the folder when empty, then forgets its folder object whether or not the folder
    // stayed. A folder in a share has no index key, so it has no folder object to forget.
    private async Task<HttpAnswer> DeleteFolderAsync(S3Reply reply, S3RequestTarget target, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        if (store.RemoveEmptyDirectory(mapping) == ContentChangeResult.NotPermitted)
        {
            return reply.Error(S3Error.AccessDenied);
        }

        var indexKey = S3IndexKey.OfFolderObject(store, Uri.UnescapeDataString(target.RawBucket), Uri.UnescapeDataString(target.RawKey));
        if (indexKey is not null && await index.ForgetFolderObjectAsync(indexKey, cancellationToken) is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }

        return reply.Answer(NoContent, [], HttpAnswerBody.None);
    }
}