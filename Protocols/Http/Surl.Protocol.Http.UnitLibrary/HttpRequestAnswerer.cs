using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// Decides the answer to each request on one connection, as <see cref="HttpProtocolServer"/>
/// describes, and writes nothing: the status, the fields, the body and whether the request
/// is refused (ADR-0081, decision 8). It never asks which HTTP version carried the request;
/// a version's writer, such as <see cref="Http1ConnectionServer"/>'s, turns the answer into bytes.
/// A <c>GET</c>'s <c>Range</c> is answered <c>206</c> or <c>416</c>, and under
/// <c>--allow-uploads</c> a <c>PUT</c> stores its body (ADR-0133).
/// </summary>
internal sealed partial class HttpRequestAnswerer : IHttpRequestHandler
{
    private const string AllowedMethods = "GET, HEAD";

    private const string AllowedMethodsWithUploads = "GET, HEAD, PUT";

    private static readonly HttpStatus PartialContent = new(206, "Partial Content");

    private static readonly HttpStatus RangeNotSatisfiable = new(416, "Range Not Satisfiable");

    private const string AbsoluteFormPrefix = "http://";

    private static readonly string[] MethodsRefusedWithAllow = ["POST", "PUT", "DELETE", "CONNECT", "OPTIONS", "TRACE", "PATCH"];

    private readonly ExchangeContext context;
    private readonly ContentStore contentStore;
    private readonly IHttpAuthenticationSession authenticationSession;

    /// <summary>
    /// Creates an answerer for one connection.
    /// </summary>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="contentStore">Where request paths are looked up.</param>
    /// <param name="authenticationSession">The connection's authentication session, which judges every request.</param>
    public HttpRequestAnswerer(ExchangeContext context, ContentStore contentStore, IHttpAuthenticationSession authenticationSession)
    {
        this.context = context;
        this.contentStore = contentStore;
        this.authenticationSession = authenticationSession;
    }

    /// <summary>
    /// Decides the answer to a request, which the authentication session judges first,
    /// reading or discarding its body as the answer needs.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var verdict = await authenticationSession.JudgeAsync(AuthenticationRequest(request), cancellationToken);

        return await AnswerVerdictAsync(request, verdict, cancellationToken);
    }

    private Task<HttpAnswer> AnswerVerdictAsync(HttpRequest request, HttpAuthenticationVerdict verdict, CancellationToken cancellationToken)
    {
        if (verdict.CheckedLogin is not null)
        {
            context.Log.Note(verdict.CheckedLogin.Note);
        }

        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed)
        {
            return Task.FromResult(RefuseLogin(request, verdict.Outcome, verdict.WwwAuthenticateValues));
        }

        return verdict.BodyCheck is null
            ? AnswerLetInRequestAsync(request, verdict.AccountName, verdict.WwwAuthenticateValues, cancellationToken)
            : AnswerBodyCheckedRequestAsync(request, verdict.BodyCheck, verdict.AccountName, verdict.WwwAuthenticateValues, cancellationToken);
    }

    // A login that binds the body (ADR-0045): the upload limit first, then the body read and
    // hashed, then the verdict the body check gives for its SHA-256, answered with the body
    // already read. A body that cannot be read cannot be checked, and is refused as malformed.
    // A PUT under --allow-uploads is read into its upload as it is hashed, and stored only once
    // the body check accepts it (ADR-0133, decision 2).
    private async Task<HttpAnswer> AnswerBodyCheckedRequestAsync(HttpRequest request, IHttpRequestBodyCheck bodyCheck, string? accountName, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        var uploadRefusal = RefuseUploadPastLimit(request, challenges);
        if (uploadRefusal is not null)
        {
            return uploadRefusal;
        }

        if (!request.Body.IsReadable)
        {
            return Refuse(HttpStatus.BadRequest, null, challenges, $"{request.Method} {request.Target}: the login is bound to the body, and the body's framing is not one the server reads");
        }

        if (IsServedUpload(request))
        {
            return await AnswerPutAsync(request, ContentCallerOf(accountName), bodyCheck, challenges, cancellationToken);
        }

        using var bodyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var bodyOutcome = await request.Body.DiscardAsync(bodyHash, cancellationToken);
        if (bodyOutcome != HttpRequestBodyDiscardOutcome.Discarded)
        {
            return RefuseBody(request, bodyOutcome, challenges);
        }

        var verdict = await bodyCheck.JudgeBodyAsync(bodyHash.GetHashAndReset(), cancellationToken);

        return await AnswerVerdictAsync(request, verdict, cancellationToken);
    }

    // A request the authentication session let in, as the account it logged in (null when
    // anonymous): a fault rule's chosen error, the upload limit, then method dispatch.
    private async Task<HttpAnswer> AnswerLetInRequestAsync(HttpRequest request, string? accountName, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        var refusal = await AnswerChosenErrorAsync(request, accountName, challenges) ?? RefuseUploadPastLimit(request, challenges);
        if (refusal is not null)
        {
            return refusal;
        }

        if (request.Method is "GET" or "HEAD")
        {
            return await AnswerFromContentStoreAsync(request, ContentCallerOf(accountName), challenges, cancellationToken);
        }

        return IsServedUpload(request)
            ? await AnswerPutAsync(request, ContentCallerOf(accountName), null, challenges, cancellationToken)
            : RefuseMethod(request, challenges);
    }

    // Who the content store is told asks: the account the verdict names, or an anonymous caller,
    // on the scheme the server listens on (ADR-0102 decision 2.2). A share the caller may not
    // read is answered as absent, so a GET or HEAD in it is the 404 a missing path gets
    // (ADR-0102 decision 2.3); a PUT the store does not permit is the 403 ADR-0133 gives it.
    private ContentCaller ContentCallerOf(string? accountName) =>
        string.IsNullOrEmpty(accountName)
            ? ContentCaller.Anonymous(context.Scheme)
            : ContentCaller.ForAccount(accountName, context.Scheme);

    // A body the server cannot read is left unread, and the answer says so.
    private async Task<HttpAnswer> AnswerFromContentStoreAsync(HttpRequest request, ContentCaller caller, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        var bodyOutcome = request.Body.IsReadable
            ? await request.Body.DiscardAsync(null, cancellationToken)
            : HttpRequestBodyDiscardOutcome.Discarded;
        if (bodyOutcome != HttpRequestBodyDiscardOutcome.Discarded)
        {
            return RefuseBody(request, bodyOutcome, challenges);
        }

        var mapping = contentStore.MapRequestPathForCaller(RequestPath(request.Target), caller);
        var (status, whyNotFound) = mapping.IsMapped ? LookUpFileStatus(mapping) : (null, WhyNotFound(mapping));
        if (status is null)
        {
            context.Log.Note($"{request.Method} {request.Target}: 404, {whyNotFound}");

            return EmptyAnswer(request, HttpStatus.NotFound, challenges);
        }

        return FileAnswer(request, mapping, status, challenges);
    }

    // The path of an origin-form target, or of an absolute-form one (RFC 9112, section 3.2.2),
    // without its query. An absolute-form target with no path is "/".
    private static string RequestPath(string requestTarget)
    {
        var path = requestTarget;
        if (requestTarget.StartsWith(AbsoluteFormPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var pathStart = requestTarget.IndexOfAny(['/', '?'], AbsoluteFormPrefix.Length);
            path = pathStart >= 0 && requestTarget[pathStart] == '/' ? requestTarget[pathStart..] : "/";
        }

        var query = path.IndexOf('?', StringComparison.Ordinal);

        return query < 0 ? path : path[..query];
    }

    // A file whose status cannot be read is answered as one that does not exist (ADR-0023):
    // the exception's text goes to the log only, never to the client (ADR-0006, section 3).
    private (ContentFileStatus? Status, string WhyNotFound) LookUpFileStatus(ContentPathMapping mapping)
    {
        try
        {
            var status = contentStore.GetFileStatus(mapping);

            return (status, WhyNotFound(mapping));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return (null, $"{mapping.Location} could not be read ({failure.GetType().Name}: {failure.Message})");
        }
    }

    private static string WhyNotFound(ContentPathMapping mapping) => mapping switch
    {
        { IsMapped: false } => $"the path was refused ({mapping.Refusal})",
        { EntryKind: ContentEntryKind.Directory } => $"{mapping.Location} is a directory",
        _ => $"nothing exists at {mapping.Location}",
    };

    // What the authentication session is shown of a request: never its body (ADR-0032, section 6).
    private static HttpAuthenticationRequest AuthenticationRequest(HttpRequest request) => new(
        request.Method,
        request.Target,
        request.Method is not ("GET" or "HEAD"),
        request.Fields.ToArray());

    // A 401 keeps the connection as a 404 does, because NTLM and Negotiate need the same
    // connection; one whose request announced a body, which is never read, and every 403
    // are refusals (ADR-0032, section 4). The note never repeats the Authorization field.
    private HttpAnswer RefuseLogin(HttpRequest request, HttpAuthenticationOutcome outcome, IReadOnlyList<string> challenges)
    {
        if (outcome == HttpAuthenticationOutcome.Forbidden)
        {
            return Refuse(HttpStatus.Forbidden, null, challenges, $"{request.Method} {request.Target}: the authentication policy forbade the request");
        }

        if (request.Body.IsAnnounced)
        {
            return Refuse(HttpStatus.Unauthorized, null, challenges, $"{request.Method} {request.Target}: a login is needed, and the body announced was not read");
        }

        context.Log.Note($"{request.Method} {request.Target}: 401, a login is needed");

        return EmptyAnswer(request, HttpStatus.Unauthorized, challenges);
    }

    // A declared body past the upload limit is refused before method dispatch (ADR-0019) and
    // before any body byte is read, so Expect: 100-continue gets the 413 in place of
    // 100 Continue; null when it is within the limit.
    private HttpAnswer? RefuseUploadPastLimit(HttpRequest request, IReadOnlyList<string> challenges)
    {
        var maxUploadBytes = context.Limits.MaxUploadBytes;

        return request.Body.AnnouncedLength is { } length && maxUploadBytes != 0 && length > maxUploadBytes
            ? Refuse(HttpStatus.ContentTooLarge, null, challenges, $"{request.Method} {request.Target}: the {length}-byte body is past the upload limit of {maxUploadBytes} bytes")
            : null;
    }

    private HttpAnswer RefuseBody(HttpRequest request, HttpRequestBodyDiscardOutcome bodyOutcome, IReadOnlyList<string> challenges) =>
        bodyOutcome == HttpRequestBodyDiscardOutcome.TooLarge
            ? Refuse(HttpStatus.ContentTooLarge, null, challenges, $"{request.Method} {request.Target}: the chunked body went past the upload limit of {context.Limits.MaxUploadBytes} bytes")
            : Refuse(HttpStatus.BadRequest, null, challenges, $"{request.Method} {request.Target}: the body was malformed or ended early");

    private HttpAnswer RefuseMethod(HttpRequest request, IReadOnlyList<string> challenges)
    {
        var isKnown = MethodsRefusedWithAllow.Contains(request.Method, StringComparer.Ordinal);
        var status = isKnown ? HttpStatus.MethodNotAllowed : HttpStatus.NotImplemented;

        var allow = contentStore.ExposureOptions.AllowUploads ? AllowedMethodsWithUploads : AllowedMethods;

        return Refuse(status, isKnown ? allow : null, challenges, $"{request.Method} {request.Target}: the method is not served");
    }

    private HttpAnswer Refuse(HttpStatus status, string? allow, IReadOnlyList<string> challenges, string note)
    {
        context.Log.Note($"{note}; answered {status.Code} and closed.");

        return new HttpAnswer(status, EmptyAnswerFields(allow, challenges), HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: false);
    }

    private HttpAnswer EmptyAnswer(HttpRequest request, HttpStatus status, IReadOnlyList<string> challenges) =>
        new(status, EmptyAnswerFields(null, challenges), HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);

    private List<KeyValuePair<string, string>> EmptyAnswerFields(string? allow, IReadOnlyList<string> challenges)
    {
        List<KeyValuePair<string, string>> fields =
        [
            new("Date", FormatHttpDate(Now())),
            new("Server", HttpResponseHead.ServerName),
        ];
        if (allow is not null)
        {
            fields.Add(new("Allow", allow));
        }

        fields.Add(new("Content-Length", "0"));

        return AddChallenges(fields, challenges);
    }

    // A file is answered 200 with its length; a GET carries its bytes, a HEAD none. A GET whose
    // Range selects bytes is answered 206 with them - several ranges as multipart/byteranges - or
    // 416 when they are all past the end (ADR-0133, decision 1 and Amendment 1).
    private HttpAnswer FileAnswer(HttpRequest request, ContentPathMapping mapping, ContentFileStatus status, IReadOnlyList<string> challenges)
    {
        var now = Now();
        var lastModified = FormatHttpDate(status.LastModifiedUtc < now ? status.LastModifiedUtc : now);
        var ranges = request.Method == "GET" ? SelectedRanges(request, lastModified, status.Length) : null;
        if (ranges is { Count: 0 })
        {
            context.Log.Note($"{request.Method} {request.Target}: 416, the range is past the end of the {status.Length}-byte {mapping.Location}");

            return RangeNotSatisfiableAnswer(request, status.Length, challenges);
        }

        return ServedFileAnswer(request, mapping, now, lastModified, ranges, status.Length, challenges);
    }

    // The 200 or 206 for a file: the ranges selected, or the whole file.
    private HttpAnswer ServedFileAnswer(HttpRequest request, ContentPathMapping mapping, DateTimeOffset now, string lastModified, IReadOnlyList<ContentByteRange>? ranges, long fileLength, IReadOnlyList<string> challenges)
    {
        List<KeyValuePair<string, string>> fields =
        [
            new("Date", FormatHttpDate(now)),
            new("Server", HttpResponseHead.ServerName),
            new("Last-Modified", lastModified),
        ];
        var mediaType = ContentMediaTypes.Of(mapping.Location!);
        if (ranges is { Count: > 1 })
        {
            return ByteRangesAnswer(request, HttpByteRangesBody.Of(contentStore, mapping, mediaType, ranges), fields, challenges);
        }

        var range = ranges?[0];
        var servedRange = range ?? ContentByteRange.WholeFile(fileLength);
        fields.Add(new("Content-Type", mediaType));
        if (range is not null)
        {
            fields.Add(new("Content-Range", FormattableString.Invariant($"bytes {range.FirstByte}-{range.LastByte}/{range.FileLength}")));
        }

        fields.Add(new("Content-Length", servedRange.ByteCount.ToString(CultureInfo.InvariantCulture)));
        HttpAnswerBody body = request.Method == "GET"
            ? new HttpAnswerBody.ContentFile(contentStore, mapping, servedRange)
            : HttpAnswerBody.None;
        var answerStatus = range is null ? HttpStatus.Ok : PartialContent;
        context.Log.Note($"{request.Method} {request.Target}: {answerStatus.Code}, {servedRange.ByteCount} bytes of {mapping.Location}");

        return new HttpAnswer(answerStatus, AddChallenges(fields, challenges), body, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    // The range a GET's Range asks for, when its If-Range (if any) is the Last-Modified the
    // answer carries, character for character; the server sends no ETag, so an entity-tag
    // If-Range never matches and the whole file is served (RFC 9110, section 13.1.5).
    private static IReadOnlyList<ContentByteRange>? SelectedRanges(HttpRequest request, string lastModified, long fileLength)
    {
        var ifRange = FieldValue(request, "If-Range");
        if (ifRange is not null && !string.Equals(ifRange.Trim(' ', '\t'), lastModified, StringComparison.Ordinal))
        {
            return null;
        }

        return HttpRangeSelection.Select(FieldValue(request, "Range"), fileLength);
    }

    // Several ranges: 206 with a multipart/byteranges body, the file's own media type moved into
    // each part (RFC 9110, section 14.6; ADR-0133, Amendment 1).
    private HttpAnswer ByteRangesAnswer(HttpRequest request, HttpAnswerBody.ContentFileParts body, List<KeyValuePair<string, string>> fields, IReadOnlyList<string> challenges)
    {
        fields.Add(new("Content-Type", HttpByteRangesBody.ContentType));
        fields.Add(new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture)));
        context.Log.Note($"{request.Method} {request.Target}: 206, {body.Parts.Count} ranges, {body.FileByteCount} bytes of {body.Mapping.Location}");

        return new HttpAnswer(PartialContent, AddChallenges(fields, challenges), body, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    // The value of a field sent once; a field sent more than once is not one the server reads.
    private static string? FieldValue(HttpRequest request, string name)
    {
        var values = request.Fields.Where(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Select(field => field.Value).ToList();

        return values.Count == 1 ? values[0] : null;
    }

    private HttpAnswer RangeNotSatisfiableAnswer(HttpRequest request, long fileLength, IReadOnlyList<string> challenges)
    {
        List<KeyValuePair<string, string>> fields =
        [
            new("Date", FormatHttpDate(Now())),
            new("Server", HttpResponseHead.ServerName),
            new("Content-Range", FormattableString.Invariant($"bytes */{fileLength}")),
            new("Content-Length", "0"),
        ];

        return new HttpAnswer(RangeNotSatisfiable, AddChallenges(fields, challenges), HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    // One WWW-Authenticate field per value the authentication session gave, after
    // Content-Length (ADR-0032, section 6).
    private static List<KeyValuePair<string, string>> AddChallenges(List<KeyValuePair<string, string>> fields, IReadOnlyList<string> challenges)
    {
        fields.AddRange(challenges.Select(challenge => new KeyValuePair<string, string>("WWW-Authenticate", challenge)));

        return fields;
    }

    private DateTimeOffset Now() => context.TimeProvider.GetUtcNow();

    // IMF-fixdate (RFC 9110, section 5.6.7): "r" is exactly that format, in UTC.
    private static string FormatHttpDate(DateTimeOffset time) => time.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
}
