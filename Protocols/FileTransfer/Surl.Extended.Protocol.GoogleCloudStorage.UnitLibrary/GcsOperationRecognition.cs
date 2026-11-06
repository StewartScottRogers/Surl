using Surl.HttpServing;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Recognises a request's <see cref="GcsOperation"/> (ADR-0110, decisions 1 and 9): by method, by
/// whether the request names a bucket and an object, and by the exact set of its query parameter
/// names. A query parameter the operation's page does not give - a subresource such as <c>acl</c>, <c>versions</c> or a
/// signed URL's <c>X-Goog-Signature</c> - and any <c>x-goog-encryption-*</c> field make the request
/// <see cref="GcsOperation.NotImplemented"/>.
/// </summary>
internal static class GcsOperationRecognition
{
    private static readonly HashSet<string> ListParameters =
        new(["list-type", "continuation-token", "delimiter", "encoding-type", "fetch-owner", "marker", "max-keys", "prefix", "start-after"], StringComparer.Ordinal);

    private static readonly Dictionary<(string Method, string Names), GcsOperation> QueryOperations = new()
    {
        [("GET", string.Empty)] = GcsOperation.ReadObject,
        [("HEAD", string.Empty)] = GcsOperation.ReadObject,
        [("GET", "generation")] = GcsOperation.ReadObject,
        [("HEAD", "generation")] = GcsOperation.ReadObject,
        [("GET", "uploadId")] = GcsOperation.ListParts,
        [("GET", "max-parts,uploadId")] = GcsOperation.ListParts,
        [("GET", "part-number-marker,uploadId")] = GcsOperation.ListParts,
        [("GET", "max-parts,part-number-marker,uploadId")] = GcsOperation.ListParts,
        [("POST", "uploads")] = GcsOperation.InitiateMultipartUpload,
        [("POST", "uploadId")] = GcsOperation.CompleteMultipartUpload,
        [("PUT", "partNumber,uploadId")] = GcsOperation.UploadPart,
        [("PUT", "upload_id")] = GcsOperation.ResumeUpload,
        [("DELETE", "uploadId")] = GcsOperation.AbortMultipartUpload,
        [("DELETE", "upload_id")] = GcsOperation.CancelResumableUpload,
    };

    /// <summary>
    /// The operation a request asks for.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="target">Its bucket, object name and query.</param>
    /// <returns>The operation, or <see cref="GcsOperation.NotImplemented"/>.</returns>
    public static GcsOperation Of(HttpRequest request, GcsRequestTarget target)
    {
        if (request.Fields.Any(field => field.Key.StartsWith("x-goog-encryption-", StringComparison.OrdinalIgnoreCase)))
        {
            return GcsOperation.NotImplemented;
        }

        if (target.NamesTheService)
        {
            return request.Method == "GET" && target.Query is null ? GcsOperation.ListBuckets : GcsOperation.NotImplemented;
        }

        if (target.NamesNoObject)
        {
            return request.Method == "GET" && OnlyGiven(target, ListParameters) ? GcsOperation.ListObjects : GcsOperation.NotImplemented;
        }

        return target.Query is null ? WithoutQuery(request) : WithQuery(request.Method, ParameterNames(target));
    }

    private static GcsOperation WithoutQuery(HttpRequest request) => request.Method switch
    {
        "GET" or "HEAD" => GcsOperation.ReadObject,
        "PUT" when GcsRequestFields.Value(request.Fields, "x-goog-copy-source") is null => GcsOperation.PutObject,
        "DELETE" => GcsOperation.DeleteObject,
        "POST" when string.Equals(GcsRequestFields.Value(request.Fields, "x-goog-resumable"), "start", StringComparison.OrdinalIgnoreCase) => GcsOperation.StartResumableUpload,
        _ => GcsOperation.NotImplemented,
    };

    // The operation a method names with exactly these parameters (decisions 1 and 9).
    private static GcsOperation WithQuery(string method, string names) =>
        QueryOperations.GetValueOrDefault((method, names), GcsOperation.NotImplemented);

    // The parameter names, each once, in ordinal order, joined with commas.
    private static string ParameterNames(GcsRequestTarget target) =>
        string.Join(',', target.Parameters.Select(parameter => parameter.Key).Distinct().Order(StringComparer.Ordinal));

    private static bool OnlyGiven(GcsRequestTarget target, HashSet<string> allowed) =>
        target.Parameters.All(parameter => allowed.Contains(parameter.Key));
}
