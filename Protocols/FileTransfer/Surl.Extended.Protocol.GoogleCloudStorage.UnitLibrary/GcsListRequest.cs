using System.Globalization;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// A List Objects request's parameters, either listing version ("GET Bucket (List Objects)";
/// ADR-0110, decision 6).
/// </summary>
/// <param name="IsVersion2">Whether <c>list-type=2</c> was sent.</param>
/// <param name="Prefix">The <c>prefix</c> every name starts with; empty when not given.</param>
/// <param name="Delimiter">The <c>delimiter</c> that rolls names up into common prefixes, or
/// <see langword="null"/> when not given or empty.</param>
/// <param name="MaxKeys">The most names and common prefixes a page holds, 0 to <see cref="MaxPageSize"/>.</param>
/// <param name="Marker">Version 1's <c>marker</c>, or <see langword="null"/>.</param>
/// <param name="ContinuationToken">Version 2's <c>continuation-token</c> as sent, or <see langword="null"/>.</param>
/// <param name="StartAfter">Version 2's <c>start-after</c>, or <see langword="null"/>.</param>
/// <param name="EncodesAsUrl">Whether <c>encoding-type=url</c> was sent.</param>
/// <param name="FetchesOwner">Whether version 2's <c>fetch-owner=true</c> was sent.</param>
/// <param name="ResumesAfter">The name the page starts after: the marker, or the token's name,
/// which wins over <c>start-after</c>; or <see langword="null"/>.</param>
internal sealed record GcsListRequest(
    bool IsVersion2,
    string Prefix,
    string? Delimiter,
    int MaxKeys,
    string? Marker,
    string? ContinuationToken,
    string? StartAfter,
    bool EncodesAsUrl,
    bool FetchesOwner,
    string? ResumesAfter)
{
    /// <summary>
    /// The most names and common prefixes one page holds; a larger <c>max-keys</c> is served as it.
    /// </summary>
    public const int MaxPageSize = 1000;

    private static readonly string[] Version2Only = ["continuation-token", "start-after", "fetch-owner"];

    /// <summary>
    /// Reads a request's parameters; every bad value is <c>400 InvalidArgument</c>.
    /// </summary>
    /// <param name="parameters">The query's parameters, decoded.</param>
    /// <returns>The request, or <see langword="null"/> when a value is bad.</returns>
    public static GcsListRequest? Read(IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        var listType = GcsQueryParameters.Value(parameters, "list-type");
        var isVersion2 = listType == "2";
        var token = GcsQueryParameters.Value(parameters, "continuation-token");
        var startAfter = GcsQueryParameters.Value(parameters, "start-after");
        var marker = GcsQueryParameters.Value(parameters, "marker");
        var resumesAfter = isVersion2 ? (token is null ? startAfter : GcsContinuationToken.Decode(token)) : marker;
        if (!AreValid(parameters, listType, isVersion2, token, resumesAfter))
        {
            return null;
        }

        var delimiter = GcsQueryParameters.Value(parameters, "delimiter");

        return new GcsListRequest(
            isVersion2,
            GcsQueryParameters.Value(parameters, "prefix") ?? string.Empty,
            string.IsNullOrEmpty(delimiter) ? null : delimiter,
            MaxKeysOf(GcsQueryParameters.Value(parameters, "max-keys")),
            marker,
            token,
            startAfter,
            GcsQueryParameters.Value(parameters, "encoding-type") is not null,
            GcsQueryParameters.Value(parameters, "fetch-owner") == "true",
            resumesAfter);
    }

    // list-type 1 or 2; a numeric max-keys; encoding-type url; the version's own paging
    // parameters only; a token that decodes.
    private static bool AreValid(IReadOnlyList<KeyValuePair<string, string>> parameters, string? listType, bool isVersion2, string? token, string? resumesAfter)
    {
        var maxKeys = GcsQueryParameters.Value(parameters, "max-keys");
        var isVersionKnown = listType is null or "1" or "2";
        var isMaxKeysValid = maxKeys is null || long.TryParse(maxKeys, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        var isEncodingValid = GcsQueryParameters.Value(parameters, "encoding-type") is null or "url";
        var areParametersOfTheVersion = isVersion2
            ? GcsQueryParameters.Value(parameters, "marker") is null
            : Version2Only.All(name => GcsQueryParameters.Value(parameters, name) is null);
        var isTokenValid = token is null || resumesAfter is not null;

        return isVersionKnown && isMaxKeysValid && isEncodingValid && areParametersOfTheVersion && isTokenValid;
    }

    private static int MaxKeysOf(string? maxKeysText) =>
        maxKeysText is null ? MaxPageSize : (int)Math.Min(MaxPageSize, long.Parse(maxKeysText, NumberStyles.None, CultureInfo.InvariantCulture));
}
