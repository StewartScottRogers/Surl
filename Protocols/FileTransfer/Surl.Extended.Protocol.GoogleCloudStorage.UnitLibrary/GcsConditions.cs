using System.Globalization;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Evaluates GET Object's and HEAD Object's conditional fields with the page's precedence
/// ("GET Object"; ADR-0110, decision 1): <c>If-Match</c> decides over <c>If-Unmodified-Since</c>,
/// <c>If-None-Match</c> over <c>If-Modified-Since</c>, and a <c>412</c> over a <c>304</c>. A date
/// that does not parse as an HTTP date is ignored.
/// </summary>
internal static class GcsConditions
{
    /// <summary>
    /// Decides what the conditional fields of a request make of an object.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="eTag">The object's ETag, quotes included.</param>
    /// <param name="lastModifiedUtc">When the object was last written.</param>
    /// <returns>Whether the object is served, not modified, or the precondition failed.</returns>
    public static GcsConditionOutcome Evaluate(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag, DateTimeOffset lastModifiedUtc)
    {
        var lastModified = TruncatedToSeconds(lastModifiedUtc);
        if (!HoldsBeforeServing(fields, eTag, lastModified))
        {
            return GcsConditionOutcome.PreconditionFailed;
        }

        return IsNotModified(fields, eTag, lastModified) ? GcsConditionOutcome.NotModified : GcsConditionOutcome.Serve;
    }

    // If-Match, else If-Unmodified-Since.
    private static bool HoldsBeforeServing(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag, DateTimeOffset lastModified) =>
        FieldValue(fields, "If-Match") is { } ifMatch
            ? MatchesAny(ifMatch, eTag)
            : DateField(fields, "If-Unmodified-Since") is not { } unmodifiedSince || lastModified <= unmodifiedSince;

    // If-None-Match, else If-Modified-Since.
    private static bool IsNotModified(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag, DateTimeOffset lastModified) =>
        FieldValue(fields, "If-None-Match") is { } ifNoneMatch
            ? MatchesAny(ifNoneMatch, eTag)
            : DateField(fields, "If-Modified-Since") is { } modifiedSince && lastModified <= modifiedSince;

    // A list of entity tags, or *; a tag sent without its quotes matches too, as clients send it.
    private static bool MatchesAny(string tagList, string eTag) =>
        tagList.Split(',', StringSplitOptions.TrimEntries).Any(tag => tag == "*" || tag == eTag || $"\"{tag}\"" == eTag);

    private static DateTimeOffset? DateField(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        FieldValue(fields, name) is { } text
            && DateTimeOffset.TryParseExact(text, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    private static string? FieldValue(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        GcsRequestFields.Value(fields, name);

    private static DateTimeOffset TruncatedToSeconds(DateTimeOffset time) =>
        new(time.UtcTicks - (time.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
}
