using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers DeleteObject (DeleteObject; ADR-0108, decisions 1 and 3): removes the file at the key
/// and answers <c>204</c>, also for a key that does not exist.
/// </summary>
/// <remarks>
/// Deleting needs <c>--allow-uploads</c>, which the store's exposure options carry: off, every
/// DeleteObject is <c>403 AccessDenied</c> (ADR-0108, decision 4.1). A key ending in <c>/</c>
/// removes its folder only when the folder is empty; a non-empty one stays, and is still
/// <c>204</c>, as deleting a folder marker leaves the keys under it. A key naming a folder without
/// the <c>/</c>, or nothing, or a path the store cannot hold, names no object: <c>204</c>. A
/// delete the store or a share refuses is <c>403 AccessDenied</c>.
/// </remarks>
/// <param name="store">The content store the object is removed from.</param>
internal sealed class S3DeleteObject(ContentStore store)
{
    private static readonly HttpStatus NoContent = new(204, "No Content");

    /// <summary>
    /// Answers a DeleteObject whose bucket exists.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The request's bucket and key.</param>
    /// <param name="caller">Who deletes, on <c>s3</c>.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(S3Reply reply, S3RequestTarget target, ContentCaller caller)
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

        return mapping.IsMapped && Delete(mapping, target.RawKey) == ContentChangeResult.NotPermitted
            ? reply.Error(S3Error.AccessDenied)
            : reply.Answer(NoContent, [], HttpAnswerBody.None);
    }

    private ContentChangeResult Delete(ContentPathMapping mapping, string rawKey) =>
        rawKey.EndsWith('/') ? store.RemoveEmptyDirectory(mapping) : store.DeleteFile(mapping);
}
