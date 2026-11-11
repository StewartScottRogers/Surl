using System.Globalization;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// A List Blobs request's parameters ("List Blobs"; ADR-0109, decision 5.3).
/// </summary>
/// <param name="Prefix">The <c>prefix</c> every name starts with; empty when not given.</param>
/// <param name="SentPrefix">The <c>prefix</c> as sent, or <see langword="null"/>.</param>
/// <param name="Delimiter">The <c>delimiter</c> that rolls names up into blob prefixes, or
/// <see langword="null"/> when not given or empty.</param>
/// <param name="MaxResults">The most blobs and blob prefixes a page holds, 1 to <see cref="MaxPageSize"/>.</param>
/// <param name="SentMaxResults">Whether <c>maxresults</c> was sent.</param>
/// <param name="Marker">The <c>marker</c> as sent, or <see langword="null"/>.</param>
/// <param name="IncludesMetadata">Whether <c>include</c> names <c>metadata</c>.</param>
/// <param name="IncludesUncommittedBlobs">Whether <c>include</c> names <c>uncommittedblobs</c>.</param>
/// <param name="ResumesAfter">The name the marker resumes after, or <see langword="null"/>.</param>
internal sealed record AzureListRequest(
    string Prefix,
    string? SentPrefix,
    string? Delimiter,
    int MaxResults,
    bool SentMaxResults,
    string? Marker,
    bool IncludesMetadata,
    bool IncludesUncommittedBlobs,
    string? ResumesAfter)
{
    /// <summary>
    /// The most blobs and blob prefixes one page holds; a larger <c>maxresults</c> is served as it.
    /// </summary>
    public const int MaxPageSize = 5000;

    // The include values the page lists: metadata and uncommittedblobs are served, the rest are
    // accepted and add nothing, because Surl keeps no snapshots, versions, deleted blobs, tags,
    // copies, policies or holds.
    private static readonly string[] IncludeValues =
    [
        "metadata", "uncommittedblobs", "snapshots", "versions", "deleted", "deletedwithversions", "tags", "copy",
        "immutabilitypolicy", "legalhold", "permissions",
    ];

    /// <summary>
    /// Reads a request's parameters.
    /// </summary>
    /// <param name="parameters">The query's parameters, decoded.</param>
    /// <param name="refusal">The refusal a bad value is answered with; <see langword="null"/>
    /// when every value is good.</param>
    /// <returns>The request, or <see langword="null"/> when a value is bad.</returns>
    public static AzureListRequest? Read(IReadOnlyList<KeyValuePair<string, string>> parameters, out AzureRefusal? refusal)
    {
        var maxResultsText = Value(parameters, "maxresults");
        var marker = Value(parameters, "marker");
        var include = Value(parameters, "include") ?? string.Empty;
        var resumesAfter = marker is null ? null : AzureListMarker.Decode(marker);
        refusal = MaxResultsRefusal(maxResultsText) ?? IncludeRefusal(include) ?? MarkerRefusal(marker, resumesAfter);
        if (refusal is not null)
        {
            return null;
        }

        return Checked(parameters, maxResultsText, marker, include, resumesAfter);
    }

    // The request, once every value has passed its check.
    private static AzureListRequest Checked(IReadOnlyList<KeyValuePair<string, string>> parameters, string? maxResultsText, string? marker, string include, string? resumesAfter)
    {
        var prefix = Value(parameters, "prefix");

        return new AzureListRequest(
            prefix ?? string.Empty,
            prefix,
            DelimiterOf(Value(parameters, "delimiter")),
            MaxResultsOf(maxResultsText),
            maxResultsText is not null,
            marker,
            Includes(include).Contains("metadata"),
            Includes(include).Contains("uncommittedblobs"),
            resumesAfter);
    }

    private static string? DelimiterOf(string? delimiter) => string.IsNullOrEmpty(delimiter) ? null : delimiter;

    private static int MaxResultsOf(string? maxResultsText) =>
        maxResultsText is null ? MaxPageSize : (int)Math.Min(MaxPageSize, long.Parse(maxResultsText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));

    // A maxresults that is no integer is InvalidQueryParameterValue; 0 or below is out of range.
    private static AzureRefusal? MaxResultsRefusal(string? maxResultsText)
    {
        if (maxResultsText is null)
        {
            return null;
        }

        if (!long.TryParse(maxResultsText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var maxResults))
        {
            return AzureRefusal.InvalidQueryParameterValue("maxresults", maxResultsText, "The value is not an integer.");
        }

        return maxResults < 1 ? AzureRefusal.OutOfRangeQueryParameterValue("maxresults", maxResultsText, "1") : null;
    }

    private static AzureRefusal? IncludeRefusal(string include) =>
        Includes(include).FirstOrDefault(value => !IncludeValues.Contains(value)) is { } unknown
            ? AzureRefusal.InvalidQueryParameterValue("include", unknown, "The value is not one of the values List Blobs takes.")
            : null;

    private static AzureRefusal? MarkerRefusal(string? marker, string? resumesAfter) =>
        marker is not null && resumesAfter is null
            ? AzureRefusal.InvalidQueryParameterValue("marker", marker, "The value is not a marker this server returned.")
            : null;

    private static string[] Includes(string include) =>
        include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // A parameter's value by its name, case-insensitively; the first one wins.
    private static string? Value(IReadOnlyList<KeyValuePair<string, string>> parameters, string name) =>
        parameters.FirstOrDefault(parameter => parameter.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
}
