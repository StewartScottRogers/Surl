using System.Globalization;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Blob endpoint's checks on a request's headers and query that come before or after the
/// login (ADR-0109, decisions 1 and 4).
/// </summary>
internal static class AzureRequestChecks
{
    /// <summary>
    /// The earliest version a bearer request may name ("Authorize with Microsoft Entra ID").
    /// </summary>
    public const string FirstBearerVersion = "2017-11-09";

    // Headers asking for a feature the server does not have (ADR-0109, decision 1), as the
    // server names them in HeaderName.
    private static readonly string[] UnsupportedHeaders =
    [
        "x-ms-copy-source", "x-ms-lease-id", "x-ms-lease-action", "x-ms-proposed-lease-id",
        "x-ms-encryption-key", "x-ms-encryption-scope", "x-ms-acl", "x-ms-permissions", "x-ms-owner",
        "x-ms-group", "x-ms-umask", "x-ms-structured-body", "x-ms-expiry-option",
    ];

    // Query parameters naming an operation or a resource the reads do not serve.
    private static readonly string[] RefusedQueryParameters = ["comp", "restype", "snapshot", "versionid"];

    /// <summary>
    /// The required headers (decision 4): a malformed <c>x-ms-version</c> or <c>x-ms-date</c>,
    /// then an <c>Authorization</c> without <c>x-ms-version</c>.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <returns>The refusal, or null when the headers pass.</returns>
    public static AzureRefusal? CheckRequiredHeaders(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        if (MalformedHeaderRefusal(fields) is { } malformed)
        {
            return malformed;
        }

        return AzureRequestFields.Value(fields, "x-ms-version") is null && AzureRequestFields.Value(fields, "Authorization") is not null
            ? AzureRefusal.ForHeader(AzureError.MissingRequiredHeader, "x-ms-version")
            : null;
    }

    private static AzureRefusal? MalformedHeaderRefusal(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        if (AzureRequestFields.Value(fields, "x-ms-version") is { } version && !IsVersion(version))
        {
            return AzureRefusal.InvalidHeaderValue("x-ms-version", version);
        }

        return AzureRequestFields.Value(fields, "x-ms-date") is { } date && !IsHttpDate(date)
            ? AzureRefusal.InvalidHeaderValue("x-ms-date", date)
            : null;
    }

    /// <summary>
    /// The query (decision 1): a <c>comp</c>, <c>restype</c>, <c>snapshot</c> or <c>versionid</c>
    /// parameter names something the reads do not serve.
    /// </summary>
    /// <param name="target">The request's target.</param>
    /// <returns>The refusal, or null when the query passes.</returns>
    public static AzureRefusal? CheckQuery(AzureBlobTarget target)
    {
        foreach (var parameter in target.QueryParameters)
        {
            var refused = Array.Find(RefusedQueryParameters, name => name.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase));
            if (refused is not null)
            {
                return AzureRefusal.InvalidQueryParameterValue(refused, parameter.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// The headers asking for a feature the server does not have (decision 1).
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <returns>The refusal naming the first such header the server lists, or null.</returns>
    public static AzureRefusal? CheckUnsupportedHeaders(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var unsupported = Array.Find(UnsupportedHeaders, name => AzureRequestFields.Value(fields, name) is not null);

        return unsupported is null ? null : AzureRefusal.ForHeader(AzureError.UnsupportedHeader, unsupported);
    }

    /// <summary>
    /// Put Blob's <c>x-ms-blob-type</c> (decision 4): missing is <c>400 MissingRequiredHeader</c>,
    /// <c>AppendBlob</c> or <c>PageBlob</c> <c>400 UnsupportedHeader</c>, another value than
    /// <c>BlockBlob</c> <c>400 InvalidHeaderValue</c>.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <returns>The refusal, or null for <c>BlockBlob</c>.</returns>
    public static AzureRefusal? CheckBlobType(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        AzureRequestFields.Value(fields, "x-ms-blob-type") switch
        {
            null => AzureRefusal.ForHeader(AzureError.MissingRequiredHeader, "x-ms-blob-type"),
            "BlockBlob" => null,
            "AppendBlob" or "PageBlob" => AzureRefusal.ForHeader(AzureError.UnsupportedHeader, "x-ms-blob-type"),
            var other => AzureRefusal.InvalidHeaderValue("x-ms-blob-type", other),
        };

    /// <summary>
    /// Whether a well-formed version is earlier than <see cref="FirstBearerVersion"/>.
    /// </summary>
    /// <param name="version">A version that passed <see cref="CheckRequiredHeaders"/>.</param>
    /// <returns>Whether a bearer request may not name it.</returns>
    public static bool PrecedesBearer(string version) => string.CompareOrdinal(version, FirstBearerVersion) < 0;

    private static bool IsVersion(string text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool IsHttpDate(string text) =>
        DateTimeOffset.TryParseExact(text, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);
}
