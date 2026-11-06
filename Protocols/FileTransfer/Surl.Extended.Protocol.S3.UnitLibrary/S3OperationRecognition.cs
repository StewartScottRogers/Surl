using Surl.HttpServing;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Recognises a request's <see cref="S3Operation"/>.
/// </summary>
internal static class S3OperationRecognition
{
    /// <summary>
    /// The operation a request asks for.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="target">Its bucket, key and query.</param>
    /// <returns>The operation, or <see cref="S3Operation.NotImplemented"/>.</returns>
    public static S3Operation Of(HttpRequest request, S3RequestTarget target)
    {
        if (target.NamesTheService)
        {
            return request.Method == "GET" && target.Query is null ? S3Operation.ListBuckets : S3Operation.NotImplemented;
        }

        if (target.NamesABucket)
        {
            return OfBucket(request, target);
        }

        return target.NamesAnObject && target.Query is null ? OfObject(request) : S3Operation.NotImplemented;
    }

    // HeadBucket with no query; ListObjectsV2 by its list-type.
    private static S3Operation OfBucket(HttpRequest request, S3RequestTarget target)
    {
        if (target.Query is not { } query)
        {
            return request.Method == "HEAD" ? S3Operation.HeadBucket : S3Operation.NotImplemented;
        }

        return request.Method == "GET" && S3QueryParameters.Value(S3QueryParameters.Parse(query), "list-type") == "2"
            ? S3Operation.ListObjectsV2
            : S3Operation.NotImplemented;
    }

    private static S3Operation OfObject(HttpRequest request) => request.Method switch
    {
        "GET" or "HEAD" => S3Operation.ReadObject,
        "PUT" when S3RequestFields.Value(request.Fields, "x-amz-copy-source") is null => S3Operation.PutObject,
        "DELETE" => S3Operation.DeleteObject,
        _ => S3Operation.NotImplemented,
    };
}
