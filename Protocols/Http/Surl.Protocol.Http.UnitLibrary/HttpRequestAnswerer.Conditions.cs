using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The conditions of a <c>GET</c> or <c>HEAD</c> of a file and the <c>ETag</c> they compare:
/// <c>If-Match</c> and <c>If-Unmodified-Since</c> give <c>412</c> when they fail, <c>If-None-Match</c>
/// and <c>If-Modified-Since</c> give <c>304</c> when the file is as the client holds it, and a value that
/// is no HTTP date or entity-tag list is ignored (ADR-0184, decisions 2, 4 and 5; RFC 9110, section 13.2.2).
/// </summary>
internal sealed partial class HttpRequestAnswerer
{
    private static readonly HttpStatus NotModified = new(304, "Not Modified");

    private static readonly HttpStatus PreconditionFailed = new(412, "Precondition Failed");

    // IMF-fixdate, RFC 850 and asctime (RFC 9110, section 5.6.7).
    private static readonly string[] HttpDateFormats =
    [
        "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'",
        "dddd, dd-MMM-yy HH':'mm':'ss 'GMT'",
        "ddd MMM d HH':'mm':'ss yyyy",
        "ddd MMM  d HH':'mm':'ss yyyy",
    ];

    // The strong entity-tag of a file: its last-write time in UTC ticks and its length, in lower-case
    // hexadecimal (ADR-0184, decision 2). It reads no body and is not clamped to the clock.
    private static string ETagOf(ContentFileStatus status) =>
        string.Create(CultureInfo.InvariantCulture, $"\"{status.LastModifiedUtc.UtcTicks:x}-{status.Length:x}\"");

    // The 304 or 412 the request's conditions decide, or null when the file is served. The
    // modification time is compared at the second precision Last-Modified is sent at.
    private HttpAnswer? ConditionAnswer(HttpRequest request, DateTimeOffset lastModified, string eTag, IReadOnlyList<string> challenges)
    {
        var modifiedAt = new DateTimeOffset(lastModified.UtcTicks - (lastModified.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
        if (FailsPrecondition(request, modifiedAt, eTag))
        {
            return EmptyAnswer(request, PreconditionFailed, challenges);
        }

        if (IsNotModified(request, modifiedAt, eTag))
        {
            context.Log.Note($"{request.Method} {request.Target}: 304, the file is as the request's validators hold it");
            List<KeyValuePair<string, string>> fields =
            [
                new("Date", FormatHttpDate(Now())),
                new("Server", HttpResponseHead.ServerName),
                new("ETag", eTag),
                new("Last-Modified", FormatHttpDate(modifiedAt)),
            ];

            return new HttpAnswer(NotModified, AddChallenges(fields, challenges), HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);
        }

        return null;
    }

    // Steps 1 and 2 of ADR-0184, decision 4: If-Match, else If-Unmodified-Since.
    private bool FailsPrecondition(HttpRequest request, DateTimeOffset modifiedAt, string eTag)
    {
        var failed = EntityTagsOf(request, "If-Match") is { } ifMatch
            ? !TagListMatches(ifMatch, eTag, isStrong: true)
            : HttpDateOf(request, "If-Unmodified-Since") is { } unmodifiedSince && modifiedAt > unmodifiedSince;
        if (failed)
        {
            context.Log.Note($"{request.Method} {request.Target}: 412, the file does not meet the request's If-Match or If-Unmodified-Since");
        }

        return failed;
    }

    // Steps 3 and 4: If-None-Match, else If-Modified-Since.
    private static bool IsNotModified(HttpRequest request, DateTimeOffset modifiedAt, string eTag) =>
        EntityTagsOf(request, "If-None-Match") is { } ifNoneMatch
            ? TagListMatches(ifNoneMatch, eTag, isStrong: false)
            : HttpDateOf(request, "If-Modified-Since") is { } modifiedSince && modifiedAt <= modifiedSince;

    // The date a field sent once holds; null when it is absent, sent twice or not an HTTP date.
    private static DateTimeOffset? HttpDateOf(HttpRequest request, string name) =>
        FieldValue(request, name) is { } value
        && DateTimeOffset.TryParseExact(value.Trim(' ', '\t'), HttpDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date
            : null;

    // The entity-tags a field sent once lists: "*" alone, or comma-separated tags, each "..." or W/"...";
    // null when it is absent, sent twice or holds anything else, so the condition is ignored.
    private static IReadOnlyList<string>? EntityTagsOf(HttpRequest request, string name)
    {
        if (FieldValue(request, name) is not { } value)
        {
            return null;
        }

        var tags = value.Split(',').Select(tag => tag.Trim(' ', '\t')).ToList();

        return tags.All(IsEntityTagOrStar) ? tags : null;
    }

    private static bool IsEntityTagOrStar(string tag)
    {
        var opaque = tag.StartsWith("W/", StringComparison.Ordinal) ? tag[2..] : tag;

        return tag == "*" || (opaque.Length >= 2 && opaque[0] == '"' && opaque[^1] == '"');
    }

    // "*" matches any file; otherwise a tag equal to the file's, weak tags excluded when the comparison is strong.
    private static bool TagListMatches(IReadOnlyList<string> tags, string eTag, bool isStrong) =>
        tags.Any(tag => tag == "*" || (isStrong ? tag == eTag : tag.StartsWith("W/", StringComparison.Ordinal) ? tag[2..] == eTag : tag == eTag));
}
