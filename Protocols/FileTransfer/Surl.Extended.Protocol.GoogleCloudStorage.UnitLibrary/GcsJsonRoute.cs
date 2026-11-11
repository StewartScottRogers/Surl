namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// What a JSON API request asks for (ADR-0148, decisions 1 and 2): recognised by the path's family
/// (<c>/storage/v1/</c>, <c>/download/storage/v1/</c> or <c>/upload/storage/v1/</c>), the method, and the exact
/// query parameters the operation takes. Any other request on a JSON path is <see cref="GcsJsonOperation.Unlisted"/>.
/// </summary>
/// <param name="Operation">The operation.</param>
/// <param name="RawBucket">The bucket as sent, still percent-encoded; empty when none is named.</param>
/// <param name="RawName">The object name as sent, with <c>%2F</c> read as <c>/</c> (decision 2), still
/// percent-encoded otherwise; empty when none is named.</param>
/// <param name="IsDownload">Whether the path is under <c>/download/storage/v1/</c>, which serves the bytes.</param>
internal sealed record GcsJsonRoute(GcsJsonOperation Operation, string RawBucket, string RawName, bool IsDownload)
{
    private const string StoragePrefix = "/storage/v1/";
    private const string DownloadPrefix = "/download/storage/v1/";
    private const string UploadPrefix = "/upload/storage/v1/";

    private static readonly string[] Common = ["alt", "fields", "key", "prettyPrint", "projection", "quotaUser", "userIp"];

    private static readonly HashSet<string> BucketsListParameters = new([.. Common, "project"], StringComparer.Ordinal);

    private static readonly HashSet<string> BucketsGetParameters = new(Common, StringComparer.Ordinal);

    private static readonly HashSet<string> ObjectsListParameters =
        new([.. Common, "delimiter", "endOffset", "includeTrailingDelimiter", "maxResults", "pageToken", "prefix", "startOffset"], StringComparer.Ordinal);

    private static readonly HashSet<string> ObjectParameters =
        new([.. Common, "generation", "ifGenerationMatch", "ifGenerationNotMatch", "ifMetagenerationMatch", "ifMetagenerationNotMatch"], StringComparer.Ordinal);

    private static readonly HashSet<string> InsertParameters =
        new([.. Common, "contentEncoding", "ifGenerationMatch", "ifGenerationNotMatch", "ifMetagenerationMatch", "ifMetagenerationNotMatch", "name", "uploadType"], StringComparer.Ordinal);

    private static readonly GcsJsonRoute UnlistedRoute = new(GcsJsonOperation.Unlisted, string.Empty, string.Empty, false);

    /// <summary>
    /// Whether the operation is one the server serves.
    /// </summary>
    public bool IsListed => Operation != GcsJsonOperation.Unlisted;

    /// <summary>
    /// The Google method id the operation is asked of the fault plan as (ADR-0148, decision 5).
    /// </summary>
    public string MethodId => Operation switch
    {
        GcsJsonOperation.ListBuckets => "storage.buckets.list",
        GcsJsonOperation.GetBucket => "storage.buckets.get",
        GcsJsonOperation.ListObjects => "storage.objects.list",
        GcsJsonOperation.GetObject => "storage.objects.get",
        GcsJsonOperation.DeleteObject => "storage.objects.delete",
        GcsJsonOperation.InsertObject => "storage.objects.insert",
        GcsJsonOperation.PatchObject => "storage.objects.patch",
        GcsJsonOperation.UpdateObject => "storage.objects.update",
        _ => "storage.unlisted",
    };

    /// <summary>
    /// Whether a path, in path-style addressing, is on the JSON API: it begins with one of the three prefixes.
    /// </summary>
    /// <param name="path">The request path as sent.</param>
    /// <returns><see langword="true"/> for a JSON path.</returns>
    public static bool IsJsonPath(string path) =>
        path.StartsWith(StoragePrefix, StringComparison.Ordinal)
        || path.StartsWith(DownloadPrefix, StringComparison.Ordinal)
        || path.StartsWith(UploadPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Recognises a JSON request.
    /// </summary>
    /// <param name="method">The request method.</param>
    /// <param name="target">The request target, on a JSON path.</param>
    /// <returns>The route.</returns>
    public static GcsJsonRoute Recognise(string method, GcsRequestTarget target)
    {
        var path = target.Path;
        var isDownload = path.StartsWith(DownloadPrefix, StringComparison.Ordinal);
        var isUpload = path.StartsWith(UploadPrefix, StringComparison.Ordinal);
        var rest = path[(isDownload ? DownloadPrefix.Length : isUpload ? UploadPrefix.Length : StoragePrefix.Length)..];
        var route = isUpload ? OfUpload(method, rest) : Of(method, rest, isDownload);

        return route.IsListed && !AreParametersOfTheOperation(route.Operation, target)
            ? UnlistedRoute
            : route;
    }

    // The upload path serves one operation: a POST to b/<bucket>/o.
    private static GcsJsonRoute OfUpload(string method, string rest)
    {
        var parts = rest.Split('/');

        return method == "POST" && parts.Length == 3 && parts[0] == "b" && parts[1].Length > 0 && parts[2] == "o"
            ? new GcsJsonRoute(GcsJsonOperation.InsertObject, parts[1], string.Empty, false)
            : UnlistedRoute;
    }

    // The operation the method names on a path after the prefix: b, b/<bucket>, b/<bucket>/o or b/<bucket>/o/<object>.
    private static GcsJsonRoute Of(string method, string rest, bool isDownload)
    {
        var parts = rest.Split('/', 4);
        if (method is not ("GET" or "DELETE" or "PATCH" or "PUT") || parts[0] != "b" || (parts.Length > 1 && parts[1].Length == 0))
        {
            return UnlistedRoute;
        }

        var operation = OperationOf(parts, method);
        var name = IsObjectOperation(operation)
            ? parts[3].Replace("%2F", "/", StringComparison.OrdinalIgnoreCase)
            : string.Empty;

        return IsServed(method, operation, isDownload)
            ? new GcsJsonRoute(operation, parts.Length > 1 ? parts[1] : string.Empty, name, isDownload)
            : UnlistedRoute;
    }

    // The operation the parts of b, b/<bucket>, b/<bucket>/o or b/<bucket>/o/<object> name; a DELETE of anything but an object is refused by IsServed.
    private static GcsJsonOperation OperationOf(string[] parts, string method)
    {
        if (parts.Length < 3)
        {
            return parts.Length == 1 ? GcsJsonOperation.ListBuckets : GcsJsonOperation.GetBucket;
        }

        if (parts[2] != "o")
        {
            return GcsJsonOperation.Unlisted;
        }

        if (parts.Length == 3)
        {
            return GcsJsonOperation.ListObjects;
        }

        if (parts[3].Length == 0)
        {
            return GcsJsonOperation.Unlisted;
        }

        return method switch
        {
            "GET" => GcsJsonOperation.GetObject,
            "DELETE" => GcsJsonOperation.DeleteObject,
            "PATCH" => GcsJsonOperation.PatchObject,
            _ => GcsJsonOperation.UpdateObject,
        };
    }

    private static bool IsObjectOperation(GcsJsonOperation operation) =>
        operation is GcsJsonOperation.GetObject or GcsJsonOperation.DeleteObject or GcsJsonOperation.PatchObject or GcsJsonOperation.UpdateObject;

    // Only GET reads buckets and lists; the other methods name an object (OperationOf); the download path serves an object's bytes only.
    private static bool IsServed(string method, GcsJsonOperation operation, bool isDownload) => operation switch
    {
        GcsJsonOperation.Unlisted => false,
        GcsJsonOperation.GetObject => method == "GET",
        _ when IsObjectOperation(operation) => !isDownload,
        _ => method == "GET" && !isDownload,
    };

    private static bool AreParametersOfTheOperation(GcsJsonOperation operation, GcsRequestTarget target)
    {
        var allowed = operation switch
        {
            GcsJsonOperation.ListBuckets => BucketsListParameters,
            GcsJsonOperation.GetBucket => BucketsGetParameters,
            GcsJsonOperation.ListObjects => ObjectsListParameters,
            GcsJsonOperation.InsertObject => InsertParameters,
            _ => ObjectParameters,
        };

        return target.Parameters.All(parameter => allowed.Contains(parameter.Key));
    }
}
