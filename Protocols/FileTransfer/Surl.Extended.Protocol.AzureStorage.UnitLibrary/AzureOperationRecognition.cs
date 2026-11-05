using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Recognises the operation a Blob endpoint request names, by its method, whether its path names a
/// container and a blob, and its query (ADR-0109, decision 1), or the page's refusal for a request
/// that names none the server serves.
/// </summary>
internal static class AzureOperationRecognition
{
    /// <summary>
    /// Recognises a request's operation. In order: List Blobs; a method no blob takes
    /// (<c>405 UnsupportedHttpVerb</c>); the query (<c>400 InvalidQueryParameterValue</c>); a path
    /// that names no blob (<c>400 InvalidUri</c>); a header for a feature the server lacks
    /// (<c>400 UnsupportedHeader</c>); Put Blob's <c>x-ms-blob-type</c>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="target">The request's target.</param>
    /// <param name="operation">The operation, when the request names one the server serves.</param>
    /// <returns>The refusal, or null when <paramref name="operation"/> is served.</returns>
    public static AzureRefusal? Recognize(HttpRequest request, AzureBlobTarget target, out AzureOperation operation)
    {
        if (IsListBlobs(request, target))
        {
            operation = AzureOperation.ListBlobs;
            return null;
        }

        operation = BlobOperationOf(request.Method);

        return IsBlobMethod(request.Method) ? BlobRefusal(request, target, operation) : AzureRefusal.Of(AzureError.UnsupportedHttpVerb);
    }

    private static AzureOperation BlobOperationOf(string method) => method switch
    {
        "PUT" => AzureOperation.PutBlob,
        "DELETE" => AzureOperation.DeleteBlob,
        _ => AzureOperation.ReadBlob,
    };

    private static bool IsBlobMethod(string method) => method is "GET" or "HEAD" or "PUT" or "DELETE";

    // The query, the path, the unsupported headers, then Put Blob's blob type.
    private static AzureRefusal? BlobRefusal(HttpRequest request, AzureBlobTarget target, AzureOperation operation)
    {
        if (AzureRequestChecks.CheckQuery(target) is { } queryRefusal)
        {
            return queryRefusal;
        }

        if (!target.NamesABlob)
        {
            return AzureRefusal.Of(AzureError.InvalidUri);
        }

        if (AzureRequestChecks.CheckUnsupportedHeaders(request.Fields) is { } headerRefusal)
        {
            return headerRefusal;
        }

        return operation == AzureOperation.PutBlob ? AzureRequestChecks.CheckBlobType(request.Fields) : null;
    }

    // GET on a container alone with restype=container and comp=list.
    private static bool IsListBlobs(HttpRequest request, AzureBlobTarget target) =>
        request.Method == "GET"
            && target.RawContainer.Length > 0
            && target.RawBlob.Length == 0
            && HasParameter(target, "restype", "container")
            && HasParameter(target, "comp", "list");

    private static bool HasParameter(AzureBlobTarget target, string name, string value) =>
        target.QueryParameters.Any(parameter => parameter.Key.Equals(name, StringComparison.OrdinalIgnoreCase) && parameter.Value == value);
}
