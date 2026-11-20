using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The date conditions of a <c>GET</c> or <c>HEAD</c> of a file: <c>If-Unmodified-Since</c> gives
/// <c>412</c> when the file is newer, <c>If-Modified-Since</c> gives <c>304</c> when it is not, and a
/// value that is no HTTP date is ignored (ADR-0184, decisions 4 and 5; RFC 9110, section 13.2.2).
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

    // The 304 or 412 the request's date conditions decide, or null when the file is served. The
    // modification time is compared at the second precision Last-Modified is sent at.
    private HttpAnswer? DateConditionAnswer(HttpRequest request, DateTimeOffset lastModified, IReadOnlyList<string> challenges)
    {
        var modifiedAt = new DateTimeOffset(lastModified.UtcTicks - (lastModified.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
        if (HttpDateOf(request, "If-Unmodified-Since") is { } unmodifiedSince && modifiedAt > unmodifiedSince)
        {
            context.Log.Note($"{request.Method} {request.Target}: 412, the file changed after the If-Unmodified-Since date");

            return EmptyAnswer(request, PreconditionFailed, challenges);
        }

        if (HttpDateOf(request, "If-Modified-Since") is { } modifiedSince && modifiedAt <= modifiedSince)
        {
            context.Log.Note($"{request.Method} {request.Target}: 304, the file has not changed since the If-Modified-Since date");
            List<KeyValuePair<string, string>> fields =
            [
                new("Date", FormatHttpDate(Now())),
                new("Server", HttpResponseHead.ServerName),
                new("Last-Modified", FormatHttpDate(modifiedAt)),
            ];

            return new HttpAnswer(NotModified, AddChallenges(fields, challenges), HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);
        }

        return null;
    }

    // The date a field sent once holds; null when it is absent, sent twice or not an HTTP date.
    private static DateTimeOffset? HttpDateOf(HttpRequest request, string name) =>
        FieldValue(request, name) is { } value
        && DateTimeOffset.TryParseExact(value.Trim(' ', '\t'), HttpDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date
            : null;
}
