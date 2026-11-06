using System.Globalization;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// A ListObjectsV2 request's parameters (ListObjectsV2; ADR-0108, decision 6).
/// </summary>
/// <param name="Prefix">The <c>prefix</c> every key starts with; empty when not given.</param>
/// <param name="Delimiter">The <c>delimiter</c> that rolls keys up into common prefixes, or
/// <see langword="null"/> when not given or empty.</param>
/// <param name="MaxKeys">The most keys and common prefixes a page holds, 0 to <see cref="MaxPageSize"/>.</param>
/// <param name="ContinuationToken">The <c>continuation-token</c> as sent, or <see langword="null"/>.</param>
/// <param name="StartAfter">The <c>start-after</c> key, or <see langword="null"/>.</param>
/// <param name="EncodesAsUrl">Whether <c>encoding-type=url</c> was sent.</param>
/// <param name="FetchesOwner">Whether <c>fetch-owner=true</c> was sent.</param>
/// <param name="ResumesAfter">The name the page starts after: the token's, which wins over
/// <c>start-after</c>, else <c>start-after</c>, else <see langword="null"/>.</param>
internal sealed record S3ListRequest(
    string Prefix,
    string? Delimiter,
    int MaxKeys,
    string? ContinuationToken,
    string? StartAfter,
    bool EncodesAsUrl,
    bool FetchesOwner,
    string? ResumesAfter)
{
    /// <summary>
    /// The most keys and common prefixes one page holds; a larger <c>max-keys</c> is served as it.
    /// </summary>
    public const int MaxPageSize = 1000;

    /// <summary>
    /// Reads a request's parameters.
    /// </summary>
    /// <param name="parameters">The query's parameters, decoded.</param>
    /// <param name="refusal">The error a bad value is answered with, and its extra elements;
    /// <see langword="null"/> when every value is good.</param>
    /// <returns>The request, or <see langword="null"/> when a value is bad.</returns>
    public static S3ListRequest? Read(IReadOnlyList<KeyValuePair<string, string>> parameters, out S3Refusal? refusal)
    {
        var token = S3QueryParameters.Value(parameters, "continuation-token");
        var startAfter = S3QueryParameters.Value(parameters, "start-after");
        var encodingType = S3QueryParameters.Value(parameters, "encoding-type");
        var maxKeysText = S3QueryParameters.Value(parameters, "max-keys");
        var resumesAfter = token is null ? startAfter : S3ContinuationToken.Decode(token);
        refusal = Refusal(maxKeysText, encodingType, token, resumesAfter);
        if (refusal is not null)
        {
            return null;
        }

        var delimiter = S3QueryParameters.Value(parameters, "delimiter");

        return new S3ListRequest(
            S3QueryParameters.Value(parameters, "prefix") ?? string.Empty,
            string.IsNullOrEmpty(delimiter) ? null : delimiter,
            MaxKeysOf(maxKeysText),
            token,
            startAfter,
            encodingType is not null,
            S3QueryParameters.Value(parameters, "fetch-owner") == "true",
            resumesAfter);
    }

    private static S3Refusal? Refusal(string? maxKeysText, string? encodingType, string? token, string? resumesAfter)
    {
        if (maxKeysText is not null && !long.TryParse(maxKeysText, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return new S3Refusal(S3Error.InvalidArgument, [new("ArgumentName", "max-keys"), new("ArgumentValue", maxKeysText)]);
        }

        if (encodingType is not (null or "url"))
        {
            return new S3Refusal(S3Error.InvalidArgument, [new("ArgumentName", "encoding-type"), new("ArgumentValue", encodingType)]);
        }

        return token is not null && resumesAfter is null
            ? new S3Refusal(S3Error.InvalidContinuationToken, [new("ArgumentName", "continuation-token"), new("ArgumentValue", token)])
            : null;
    }

    private static int MaxKeysOf(string? maxKeysText) =>
        maxKeysText is null ? MaxPageSize : (int)Math.Min(MaxPageSize, long.Parse(maxKeysText, NumberStyles.None, CultureInfo.InvariantCulture));
}
