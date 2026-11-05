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
        if (target.NamesABucket)
        {
            return request.Method == "GET" && target.Query is { } query && S3QueryParameters.Value(S3QueryParameters.Parse(query), "list-type") == "2"
                ? S3Operation.ListObjectsV2
                : S3Operation.NotImplemented;
        }

        if (!target.NamesAnObject || target.Query is not null)
        {
            return S3Operation.NotImplemented;
        }

        return request.Method switch
        {
            "GET" or "HEAD" => S3Operation.ReadObject,
            "PUT" when S3RequestFields.Value(request.Fields, "x-amz-copy-source") is null => S3Operation.PutObject,
            _ => S3Operation.NotImplemented,
        };
    }
}
