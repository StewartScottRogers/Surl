using Surl.HttpServing;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Recognises a request's <see cref="GcsOperation"/> (ADR-0110, decision 1). A query parameter
/// the operation's page does not give - a subresource such as <c>acl</c>, <c>versions</c> or a
/// signed URL's <c>X-Goog-Signature</c> - and any <c>x-goog-encryption-*</c> field make the request
/// <see cref="GcsOperation.NotImplemented"/>.
/// </summary>
internal static class GcsOperationRecognition
{
    private static readonly HashSet<string> ListParameters =
        new(["list-type", "continuation-token", "delimiter", "encoding-type", "fetch-owner", "marker", "max-keys", "prefix", "start-after"], StringComparer.Ordinal);

    private static readonly HashSet<string> ReadParameters = new(["generation"], StringComparer.Ordinal);

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

        return request.Method switch
        {
            "GET" or "HEAD" when OnlyGiven(target, ReadParameters) => GcsOperation.ReadObject,
            "PUT" when target.Query is null && GcsRequestFields.Value(request.Fields, "x-goog-copy-source") is null => GcsOperation.PutObject,
            "DELETE" when target.Query is null => GcsOperation.DeleteObject,
            _ => GcsOperation.NotImplemented,
        };
    }

    private static bool OnlyGiven(GcsRequestTarget target, HashSet<string> allowed) =>
        target.Parameters.All(parameter => allowed.Contains(parameter.Key));
}
