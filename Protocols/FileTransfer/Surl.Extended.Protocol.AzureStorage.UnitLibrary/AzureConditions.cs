using System.Globalization;
using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Evaluates the conditional fields: a write's with <see cref="WriteRefusal"/>, and Get Blob's and Get Blob Properties' with the page's precedence
/// ("Specifying conditional headers for Blob service operations"; ADR-0109, decision 1): <c>If-Match</c> decides over <c>If-Unmodified-Since</c>,
/// <c>If-None-Match</c> over <c>If-Modified-Since</c>, and a <c>412</c> over a <c>304</c>. A date
/// that does not parse as an HTTP date is ignored.
/// </summary>
internal static class AzureConditions
{
    /// <summary>
    /// Decides what the conditional fields of a request make of a blob.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="eTag">The blob's ETag, quotes included.</param>
    /// <param name="lastModifiedUtc">When the blob was last written.</param>
    /// <returns>Whether the blob is served, not modified, or the precondition failed.</returns>
    public static AzureConditionOutcome Evaluate(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag, DateTimeOffset lastModifiedUtc)
    {
        var lastModified = TruncatedToSeconds(lastModifiedUtc);
        if (!HoldsBeforeServing(fields, eTag, lastModified))
        {
            return AzureConditionOutcome.ConditionNotMet;
        }

        return IsNotModified(fields, eTag, lastModified) ? AzureConditionOutcome.NotModified : AzureConditionOutcome.Serve;
    }

    /// <summary>
    /// Decides what the conditional fields of a write - Put Blob or Delete Blob - make of the blob
    /// at its name (ADR-0109, decision 1): every condition is checked, none decides over another.
    /// On an existing blob, <c>If-None-Match</c> naming <c>*</c> is <c>409 BlobAlreadyExists</c>, and a
    /// failing <c>If-Match</c> or <c>If-Unmodified-Since</c>, a matching <c>If-None-Match</c> or an
    /// unmodified <c>If-Modified-Since</c> is <c>412 ConditionNotMet</c>. With no blob there, only
    /// <c>If-Match</c> fails, as there is no tag for it to match.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="existing">The blob at the name, or <see langword="null"/> when there is none.</param>
    /// <returns><see langword="null"/> when the write goes ahead, else its error.</returns>
    public static AzureError? WriteRefusal(IReadOnlyList<KeyValuePair<string, string>> fields, ObjectProperties? existing) =>
        EntryWriteRefusal(fields, existing is null ? null : (existing.ETag, existing.LastWriteTimeUtc), AzureError.BlobAlreadyExists);

    /// <summary>
    /// Decides what the conditional fields of a write make of the entry at its name, as
    /// <see cref="WriteRefusal"/>
    /// does, with <paramref name="alreadyExists"/> for <c>If-None-Match: *</c> on an existing entry:
    /// the Data Lake writes' <c>409 PathAlreadyExists</c> (ADR-0109, decision 1).
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="existing">The entry's ETag and last write time, or <see langword="null"/> when there is none.</param>
    /// <param name="alreadyExists">The error <c>If-None-Match: *</c> on an existing entry is.</param>
    /// <returns><see langword="null"/> when the write goes ahead, else its error.</returns>
    public static AzureError? EntryWriteRefusal(IReadOnlyList<KeyValuePair<string, string>> fields, (string ETag, DateTimeOffset LastModifiedUtc)? existing, AzureError alreadyExists)
    {
        if (existing is not { } entry)
        {
            return FieldValue(fields, "If-Match") is null ? null : AzureError.ConditionNotMet;
        }

        if (FieldValue(fields, "If-None-Match") is { } ifNoneMatch && ifNoneMatch.Split(',', StringSplitOptions.TrimEntries).Contains("*"))
        {
            return alreadyExists;
        }

        return HoldsForWriting(fields, entry.ETag, TruncatedToSeconds(entry.LastModifiedUtc)) ? null : AzureError.ConditionNotMet;
    }

    // If-Match and If-Unmodified-Since hold, and If-None-Match and If-Modified-Since do not stop the write.
    private static bool HoldsForWriting(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag, DateTimeOffset lastModified) =>
        TagsHoldForWriting(fields, eTag) && DatesHoldForWriting(fields, lastModified);

    private static bool TagsHoldForWriting(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag) =>
        (FieldValue(fields, "If-Match") is not { } ifMatch || MatchesAny(ifMatch, eTag))
            && (FieldValue(fields, "If-None-Match") is not { } ifNoneMatch || !MatchesAny(ifNoneMatch, eTag));

    private static bool DatesHoldForWriting(IReadOnlyList<KeyValuePair<string, string>> fields, DateTimeOffset lastModified) =>
        (DateField(fields, "If-Unmodified-Since") is not { } unmodifiedSince || lastModified <= unmodifiedSince)
            && (DateField(fields, "If-Modified-Since") is not { } modifiedSince || lastModified > modifiedSince);

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

    // A list of entity tags, or *; a tag sent without its quotes matches too, as a lenient server accepts it.
    private static bool MatchesAny(string tagList, string eTag) =>
        tagList.Split(',', StringSplitOptions.TrimEntries).Any(tag => tag == "*" || tag == eTag || $"\"{tag}\"" == eTag);

    private static DateTimeOffset? DateField(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        FieldValue(fields, name) is { } text
            && DateTimeOffset.TryParseExact(text, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    private static string? FieldValue(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        AzureRequestFields.Value(fields, name);

    private static DateTimeOffset TruncatedToSeconds(DateTimeOffset time) =>
        new(time.UtcTicks - (time.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
}
