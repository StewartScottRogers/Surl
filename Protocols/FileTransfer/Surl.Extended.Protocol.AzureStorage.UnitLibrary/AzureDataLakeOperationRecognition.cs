using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Recognises the operation a Data Lake endpoint request names, by its method, whether its path
/// names a filesystem and a path, and its <c>resource</c> and <c>action</c> parameters (ADR-0109,
/// decision 1), or the page's refusal for a request that names none the server serves.
/// </summary>
internal static class AzureDataLakeOperationRecognition
{
    /// <summary>
    /// Recognises a request's operation. In order: Filesystem - List, Path - List and Filesystem - Get
    /// Properties; a method no path takes
    /// (<c>405 UnsupportedHttpVerb</c>); a path that names no file or directory (<c>400
    /// InvalidQueryParameterValue</c> for its <c>resource</c>, else <c>400 InvalidUri</c>); a header
    /// for a feature the server lacks (<c>400 UnsupportedHeader</c>); Path - Create's
    /// <c>resource</c> and Path - Update's <c>action</c> (<c>400 MissingRequiredQueryParameter</c>,
    /// <c>400 InvalidQueryParameterValue</c>), or a read's <c>action</c> other than <c>getStatus</c>.
    /// A <c>PUT</c> with <c>x-ms-rename-source</c> is a rename and needs no <c>resource</c>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="target">The request's target: the filesystem and the path.</param>
    /// <param name="operation">The operation, when the request names one the server serves.</param>
    /// <returns>The refusal, or null when <paramref name="operation"/> is served.</returns>
    public static AzureRefusal? Recognize(HttpRequest request, AzureBlobTarget target, out AzureDataLakeOperation operation)
    {
        if (AccountOrFilesystemOperationOf(request.Method, target) is { } listOrProperties)
        {
            operation = listOrProperties;
            return null;
        }

        operation = OperationOf(request, target);

        return IsPathMethod(request.Method) ? PathRefusal(request, target, operation) : AzureRefusal.Of(AzureError.UnsupportedHttpVerb);
    }

    /// <summary>
    /// The value of the query parameter named <paramref name="name"/>, or null when it is absent.
    /// </summary>
    /// <param name="target">The request's target.</param>
    /// <param name="name">The parameter's name, matched case-insensitively.</param>
    /// <returns>The value, or null.</returns>
    public static string? Parameter(AzureBlobTarget target, string name) =>
        target.QueryParameters.FirstOrDefault(parameter => parameter.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    // A PUT with x-ms-rename-source is a rename, and a PATCH with action=setProperties sets properties.
    private static AzureDataLakeOperation OperationOf(HttpRequest request, AzureBlobTarget target) => request.Method switch
    {
        "PUT" when AzureRequestFields.Value(request.Fields, AzurePathRename.SourceField) is not null => AzureDataLakeOperation.RenamePath,
        "PUT" => AzureDataLakeOperation.CreatePath,
        "PATCH" when Parameter(target, "action") == "setProperties" => AzureDataLakeOperation.SetPathProperties,
        "PATCH" => AzureDataLakeOperation.UpdatePath,
        "DELETE" => AzureDataLakeOperation.DeletePath,
        _ => AzureDataLakeOperation.ReadPath,
    };

    private static bool IsPathMethod(string method) => method is "GET" or "HEAD" or "PUT" or "PATCH" or "DELETE";

    // The path, the unsupported headers, then the parameter that names the operation.
    private static AzureRefusal? PathRefusal(HttpRequest request, AzureBlobTarget target, AzureDataLakeOperation operation)
    {
        if (!target.NamesABlob)
        {
            return Parameter(target, "resource") is { } resource
                ? AzureRefusal.InvalidQueryParameterValue("resource", resource)
                : AzureRefusal.Of(AzureError.InvalidUri);
        }

        if (AzureRequestChecks.CheckUnsupportedHeaders(request.Fields) is { } headerRefusal)
        {
            return headerRefusal;
        }

        return operation switch
        {
            AzureDataLakeOperation.CreatePath => CheckNamingParameter(target, "resource", ["file", "directory"]),
            AzureDataLakeOperation.UpdatePath => CheckNamingParameter(target, "action", ["append", "flush"]),
            AzureDataLakeOperation.ReadPath => CheckReadAction(target),
            _ => null,
        };
    }

    // Path - Read and Path - Get Properties take no action but getStatus ("Path - Get Properties"):
    // getAccessControl and any other is not served.
    private static AzureRefusal? CheckReadAction(AzureBlobTarget target) =>
        Parameter(target, "action") is { } action && action != "getStatus" ? AzureRefusal.InvalidQueryParameterValue("action", action) : null;

    // The parameter naming what a PUT creates or what a PATCH does: missing, or a value not served.
    private static AzureRefusal? CheckNamingParameter(AzureBlobTarget target, string name, string[] served)
    {
        var value = Parameter(target, name);
        if (value is null)
        {
            return AzureRefusal.MissingRequiredQueryParameter(name);
        }

        return served.Contains(value) ? null : AzureRefusal.InvalidQueryParameterValue(name, value);
    }

    // Filesystem - List (GET on the account with resource=account), Path - List (GET on a filesystem
    // alone with resource=filesystem) and Filesystem - Get Properties (HEAD on it).
    private static AzureDataLakeOperation? AccountOrFilesystemOperationOf(string method, AzureBlobTarget target)
    {
        if (target.RawBlob.Length > 0)
        {
            return null;
        }

        var resource = Parameter(target, "resource");
        if (target.RawContainer.Length == 0)
        {
            return method == "GET" && resource == "account" ? AzureDataLakeOperation.ListFilesystems : null;
        }

        return (method, resource) switch
        {
            ("GET", "filesystem") => AzureDataLakeOperation.ListPaths,
            ("HEAD", "filesystem") => AzureDataLakeOperation.GetFilesystemProperties,
            _ => null,
        };
    }
}
