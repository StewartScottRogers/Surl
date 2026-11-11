using System.Globalization;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The query of <c>storage.objects.list</c> (ADR-0148, decision 3): <c>prefix</c>, <c>delimiter</c>,
/// <c>includeTrailingDelimiter</c>, <c>startOffset</c>, <c>endOffset</c>, <c>maxResults</c> and <c>pageToken</c>.
/// </summary>
/// <param name="List">The same walk and paging the XML API's listing uses: prefix, delimiter, page size and the name to resume after.</param>
/// <param name="StartOffset">The first name listed (inclusive), or <see langword="null"/>.</param>
/// <param name="EndOffset">The first name not listed (exclusive), or <see langword="null"/>.</param>
internal sealed record GcsJsonListRequest(GcsListRequest List, string? StartOffset, string? EndOffset)
{
    /// <summary>
    /// Reads the query.
    /// </summary>
    /// <param name="parameters">The query's parameters.</param>
    /// <returns>The request, or <see langword="null"/> when <c>maxResults</c> is negative or not a number,
    /// <c>includeTrailingDelimiter</c> is not <c>true</c> or <c>false</c>, or <c>pageToken</c> does not decode.</returns>
    public static GcsJsonListRequest? Read(IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        var maxResults = GcsQueryParameters.Value(parameters, "maxResults");
        var token = GcsQueryParameters.Value(parameters, "pageToken");
        var resumesAfter = string.IsNullOrEmpty(token) ? null : GcsContinuationToken.Decode(token);
        var isMaxResultsValid = maxResults is null || long.TryParse(maxResults, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        var isTrailingValid = GcsQueryParameters.Value(parameters, "includeTrailingDelimiter") is null or "true" or "false";
        if (!isMaxResultsValid || !isTrailingValid || (!string.IsNullOrEmpty(token) && resumesAfter is null))
        {
            return null;
        }

        var delimiter = GcsQueryParameters.Value(parameters, "delimiter");
        var list = new GcsListRequest(
            IsVersion2: false,
            GcsQueryParameters.Value(parameters, "prefix") ?? string.Empty,
            string.IsNullOrEmpty(delimiter) ? null : delimiter,
            maxResults is null ? GcsListRequest.MaxPageSize : (int)Math.Min(GcsListRequest.MaxPageSize, long.Parse(maxResults, NumberStyles.None, CultureInfo.InvariantCulture)),
            Marker: null,
            ContinuationToken: null,
            StartAfter: null,
            EncodesAsUrl: false,
            FetchesOwner: false,
            resumesAfter);

        return new GcsJsonListRequest(list, NonEmpty(parameters, "startOffset"), NonEmpty(parameters, "endOffset"));
    }

    /// <summary>
    /// Whether a name is within <c>startOffset</c> (inclusive) and <c>endOffset</c> (exclusive).
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <returns><see langword="true"/> when the name is listed.</returns>
    public bool Includes(string name) =>
        (StartOffset is null || GcsNameOrder.Instance.Compare(name, StartOffset) >= 0)
        && (EndOffset is null || GcsNameOrder.Instance.Compare(name, EndOffset) < 0);

    private static string? NonEmpty(IReadOnlyList<KeyValuePair<string, string>> parameters, string name) =>
        GcsQueryParameters.Value(parameters, name) is { Length: > 0 } value ? value : null;
}
