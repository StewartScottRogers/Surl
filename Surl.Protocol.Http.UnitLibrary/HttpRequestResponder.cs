using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// Writes the response to each request read from one connection, as
/// <see cref="HttpProtocolServer"/> describes, and says whether the connection stays open.
/// </summary>
internal sealed class HttpRequestResponder
{
    /// <summary>
    /// How long a refusal - any response that closes the connection because of the request -
    /// may take to write before the server gives up on it (ADR-0006, section 5).
    /// </summary>
    public static readonly TimeSpan RefusalWriteDeadline = TimeSpan.FromSeconds(1);

    private const string AllowedMethods = "GET, HEAD";

    private const string AbsoluteFormPrefix = "http://";

    private static readonly Version HttpVersion11 = new(1, 1);

    // A request whose body was read to check the login bound to it is answered as having none left to read.
    private static readonly HttpRequestBodyFraming BodyAlreadyRead = new(HttpRequestBodyFramingKind.None, 0);

    private static readonly string[] MethodsRefusedWithAllow = ["POST", "PUT", "DELETE", "CONNECT", "OPTIONS", "TRACE", "PATCH"];

    // Every other failure to read a head is a malformed head: 400.
    private static readonly Dictionary<HttpRequestHeadReadOutcome, HttpStatus> StatusesForHeadsNotRead = new()
    {
        [HttpRequestHeadReadOutcome.UnsupportedVersion] = HttpStatus.HttpVersionNotSupported,
        [HttpRequestHeadReadOutcome.HeadTooLarge] = HttpStatus.RequestHeaderFieldsTooLarge,
        [HttpRequestHeadReadOutcome.HeadTimedOut] = HttpStatus.RequestTimeout,
    };

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly ContentStore contentStore;
    private readonly HttpRequestBodyDiscarder bodyDiscarder;
    private readonly HttpUnreadRequestDrainer unreadRequestDrainer;
    private readonly IHttpAuthenticationSession authenticationSession;

    // The WWW-Authenticate values the authentication session gave for the request being
    // answered, written on whatever response it gets; none before a request is judged.
    private IReadOnlyList<string> wwwAuthenticateValues = [];

    /// <summary>
    /// Creates a responder for one connection.
    /// </summary>
    /// <param name="connection">Where responses are written.</param>
    /// <param name="reader">The connection's reader, through which request bodies are read.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="contentStore">Where request paths are looked up.</param>
    /// <param name="authenticationSession">The connection's authentication session, which judges every request.</param>
    public HttpRequestResponder(IConnection connection, HttpConnectionReader reader, ExchangeContext context, ContentStore contentStore, IHttpAuthenticationSession authenticationSession)
    {
        this.connection = connection;
        this.context = context;
        this.contentStore = contentStore;
        this.authenticationSession = authenticationSession;
        bodyDiscarder = new HttpRequestBodyDiscarder(reader, context.Limits.MaxUploadBytes);
        unreadRequestDrainer = new HttpUnreadRequestDrainer(reader, context);
    }

    /// <summary>
    /// Answers a request whose head was read.
    /// </summary>
    /// <param name="head">The request head.</param>
    /// <returns><see langword="true"/> when the connection stays open for another request.</returns>
    public async Task<bool> AnswerRequestAsync(HttpRequestHead head)
    {
        wwwAuthenticateValues = [];
        var framing = HttpRequestBodyFraming.Of(head);
        var malformedHeadRefusal = RefuseMalformedHeadAsync(head, framing);
        if (malformedHeadRefusal is not null)
        {
            return await malformedHeadRefusal;
        }

        var verdict = await authenticationSession.JudgeAsync(AuthenticationRequest(head), context.CancellationToken);

        return await AnswerVerdictAsync(head, framing, verdict);
    }

    private Task<bool> AnswerVerdictAsync(HttpRequestHead head, HttpRequestBodyFraming framing, HttpAuthenticationVerdict verdict)
    {
        wwwAuthenticateValues = verdict.WwwAuthenticateValues;
        if (verdict.CheckedLogin is not null)
        {
            context.Log.Note(verdict.CheckedLogin.Note);
        }

        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed)
        {
            return RefuseLoginAsync(head, framing, verdict.Outcome);
        }

        return verdict.BodyCheck is null
            ? AnswerLetInRequestAsync(head, framing)
            : AnswerBodyCheckedRequestAsync(head, framing, verdict.BodyCheck);
    }

    // A login that binds the body (ADR-0045): the upload limit first, then the body read and
    // hashed, then the verdict the body check gives for its SHA-256, answered with the body
    // already read. A body that cannot be read cannot be checked, and is refused as malformed.
    private async Task<bool> AnswerBodyCheckedRequestAsync(HttpRequestHead head, HttpRequestBodyFraming framing, IHttpRequestBodyCheck bodyCheck)
    {
        var uploadRefusal = RefuseUploadPastLimitAsync(head, framing);
        if (uploadRefusal is not null)
        {
            return await uploadRefusal;
        }

        if (framing.Kind == HttpRequestBodyFramingKind.Unreadable)
        {
            return await RefuseAsync(HttpStatus.BadRequest, null, $"{head.Method} {head.RequestTarget}: the login is bound to the body, and the body's framing is not one the server reads");
        }

        using var bodyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var bodyOutcome = await DiscardBodyAsync(head, framing, bodyHash);
        if (bodyOutcome != HttpRequestBodyDiscardOutcome.Discarded)
        {
            return await RefuseBodyAsync(head, bodyOutcome);
        }

        var verdict = await bodyCheck.JudgeBodyAsync(bodyHash.GetHashAndReset(), context.CancellationToken);

        return await AnswerVerdictAsync(head, BodyAlreadyRead, verdict);
    }

    /// <summary>
    /// Answers a connection on which no request head could be read: nothing when the client
    /// closed it or never sent a byte before the head timeout, otherwise a refusal that
    /// closes it.
    /// </summary>
    /// <param name="outcome">Why no head was read.</param>
    /// <returns><see langword="false"/>: the connection never stays open after this.</returns>
    public Task<bool> AnswerHeadNotReadAsync(HttpRequestHeadReadOutcome outcome)
    {
        wwwAuthenticateValues = [];
        if (outcome is HttpRequestHeadReadOutcome.ConnectionClosed or HttpRequestHeadReadOutcome.ConnectionClosedBeforeHeadEnded)
        {
            context.Log.Note($"The client closed the connection: {outcome}.");

            return Task.FromResult(false);
        }

        if (outcome is HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte)
        {
            context.Log.Note($"No request head was read: {outcome}; closed with no bytes.");

            return Task.FromResult(false);
        }

        var status = StatusesForHeadsNotRead.GetValueOrDefault(outcome, HttpStatus.BadRequest);

        return RefuseAsync(status, null, $"No request head was read: {outcome}");
    }

    private async Task<bool> AnswerFromContentStoreAsync(HttpRequestHead head, HttpRequestBodyFraming framing)
    {
        var bodyOutcome = framing.Kind == HttpRequestBodyFramingKind.Unreadable
            ? HttpRequestBodyDiscardOutcome.Discarded
            : await DiscardBodyAsync(head, framing, null);
        if (bodyOutcome != HttpRequestBodyDiscardOutcome.Discarded)
        {
            return await RefuseBodyAsync(head, bodyOutcome);
        }

        var keepsConnectionOpen = HttpConnectionPersistence.KeepsConnectionOpen(head) && framing.Kind != HttpRequestBodyFramingKind.Unreadable;
        var mapping = contentStore.MapRequestPath(RequestPath(head.RequestTarget));
        var (status, whyNotFound) = mapping.IsMapped ? LookUpFileStatus(mapping) : (null, WhyNotFound(mapping));
        if (status is null)
        {
            context.Log.Note($"{head.Method} {head.RequestTarget}: 404, {whyNotFound}");

            return await WriteEmptyResponseAsync(HttpStatus.NotFound, null, keepsConnectionOpen, head.Version);
        }

        return await WriteFileResponseAsync(head, mapping, status, keepsConnectionOpen);
    }

    // RFC 9110, section 10.1.1: a server that reads the content of a request expecting
    // 100-continue sends 100 Continue first, so the client need not wait out its own timeout
    // (curl's --expect100-timeout, one second) before it sends the body (ADR-0027).
    private async Task<HttpRequestBodyDiscardOutcome> DiscardBodyAsync(HttpRequestHead head, HttpRequestBodyFraming framing, IncrementalHash? bodyHash)
    {
        if (framing.Kind != HttpRequestBodyFramingKind.None && ExpectsContinue(head))
        {
            await connection.WriteAsync(new HttpResponseHead(HttpStatus.Continue).ToBytes(), context.CancellationToken);
        }

        return await bodyDiscarder.DiscardAsync(framing, bodyHash, context.CancellationToken);
    }

    // The expectation is ignored in an HTTP/1.0 request (RFC 9110, section 10.1.1). Expect is
    // a list, and 100-continue is matched without regard to case.
    private static bool ExpectsContinue(HttpRequestHead head) =>
        head.Version.Minor >= 1
        && head.GetFieldValues("Expect")
            .SelectMany(value => value.Split(','))
            .Any(expectation => string.Equals(expectation.Trim(' ', '\t'), "100-continue", StringComparison.OrdinalIgnoreCase));

    // RFC 9112, section 3.2: 400 for an HTTP/1.1 request without Host, and for any request with two.
    private static bool HasHostAsRequired(HttpRequestHead head)
    {
        var hostCount = head.GetFieldValues("Host").Count;

        return hostCount == 1 || (hostCount == 0 && head.Version.Minor == 0);
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
        catch (Exception failure) when (IsFileSystemFailure(failure))
        {
            return (null, $"{mapping.Location} could not be read ({failure.GetType().Name}: {failure.Message})");
        }
    }

    private static bool IsFileSystemFailure(Exception failure) => failure is IOException or UnauthorizedAccessException;

    private static string WhyNotFound(ContentPathMapping mapping) => mapping switch
    {
        { IsMapped: false } => $"the path was refused ({mapping.Refusal})",
        { EntryKind: ContentEntryKind.Directory } => $"{mapping.Location} is a directory",
        _ => $"nothing exists at {mapping.Location}",
    };

    // A head that is not well-formed - a missing or repeated Host (RFC 9112, section 3.2), a
    // Content-Length that is not one field of decimal digits (ADR-0024) - is refused before
    // the authentication session sees it (ADR-0032, section 4); null when the head is well-formed.
    private Task<bool>? RefuseMalformedHeadAsync(HttpRequestHead head, HttpRequestBodyFraming framing)
    {
        if (!HasHostAsRequired(head))
        {
            return RefuseAsync(HttpStatus.BadRequest, null, $"{head.Method} {head.RequestTarget}: a request may carry at most one Host field, and an HTTP/1.1 request needs one");
        }

        return framing.Kind == HttpRequestBodyFramingKind.InvalidContentLength
            ? RefuseAsync(HttpStatus.BadRequest, null, $"{head.Method} {head.RequestTarget}: the Content-Length is not one field of decimal digits")
            : null;
    }

    // A request the authentication session let in: the upload limit, then method dispatch.
    private Task<bool> AnswerLetInRequestAsync(HttpRequestHead head, HttpRequestBodyFraming framing) =>
        RefuseUploadPastLimitAsync(head, framing)
            ?? (head.Method is "GET" or "HEAD" ? AnswerFromContentStoreAsync(head, framing) : RefuseMethodAsync(head));

    // What the authentication session is shown of a request: never its body (ADR-0032, section 6).
    private static HttpAuthenticationRequest AuthenticationRequest(HttpRequestHead head) => new(
        head.Method,
        head.RequestTarget,
        head.Method is not ("GET" or "HEAD"),
        head.Fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)).ToArray());

    // A 401 keeps the connection as a 404 does, because NTLM and Negotiate need the same
    // connection; one whose request announced a body, which is never read, and every 403
    // are refusals (ADR-0032, section 4). The note never repeats the Authorization field.
    private Task<bool> RefuseLoginAsync(HttpRequestHead head, HttpRequestBodyFraming framing, HttpAuthenticationOutcome outcome)
    {
        if (outcome == HttpAuthenticationOutcome.Forbidden)
        {
            return RefuseAsync(HttpStatus.Forbidden, null, $"{head.Method} {head.RequestTarget}: the authentication policy forbade the request");
        }

        if (framing.Kind != HttpRequestBodyFramingKind.None)
        {
            return RefuseAsync(HttpStatus.Unauthorized, null, $"{head.Method} {head.RequestTarget}: a login is needed, and the body announced was not read");
        }

        context.Log.Note($"{head.Method} {head.RequestTarget}: 401, a login is needed");

        return WriteEmptyResponseAsync(HttpStatus.Unauthorized, null, HttpConnectionPersistence.KeepsConnectionOpen(head), head.Version);
    }

    // A declared body past the upload limit is refused before method dispatch (ADR-0019);
    // null when it is within it.
    private Task<bool>? RefuseUploadPastLimitAsync(HttpRequestHead head, HttpRequestBodyFraming framing) =>
        IsPastUploadLimit(framing)
            ? RefuseAsync(HttpStatus.ContentTooLarge, null, $"{head.Method} {head.RequestTarget}: the {framing.ContentLength}-byte body is past the upload limit of {context.Limits.MaxUploadBytes} bytes")
            : null;

    // A declared Content-Length is checked before any body byte is read and before method
    // dispatch, so Expect: 100-continue gets the 413 in place of 100 Continue.
    private bool IsPastUploadLimit(HttpRequestBodyFraming framing) =>
        framing.Kind == HttpRequestBodyFramingKind.ContentLength
        && context.Limits.MaxUploadBytes != 0
        && framing.ContentLength > context.Limits.MaxUploadBytes;

    private Task<bool> RefuseBodyAsync(HttpRequestHead head, HttpRequestBodyDiscardOutcome bodyOutcome) =>
        bodyOutcome == HttpRequestBodyDiscardOutcome.TooLarge
            ? RefuseAsync(HttpStatus.ContentTooLarge, null, $"{head.Method} {head.RequestTarget}: the chunked body went past the upload limit of {context.Limits.MaxUploadBytes} bytes")
            : RefuseAsync(HttpStatus.BadRequest, null, $"{head.Method} {head.RequestTarget}: the body was malformed or ended early");

    private Task<bool> RefuseMethodAsync(HttpRequestHead head)
    {
        var isKnown = MethodsRefusedWithAllow.Contains(head.Method, StringComparer.Ordinal);
        var status = isKnown ? HttpStatus.MethodNotAllowed : HttpStatus.NotImplemented;

        return RefuseAsync(status, isKnown ? AllowedMethods : null, $"{head.Method} {head.RequestTarget}: the method is not served");
    }

    // A refusal gets one second to be written and half-closed; past that the server gives up
    // on it and leaves the close to the engine's dispose, never an abort (ADR-0006, section 5).
    // Once the refusal is written, what the client still sends is drained (ADR-0024).
    private async Task<bool> RefuseAsync(HttpStatus status, string? allow, string note)
    {
        context.Log.Note($"{note}; answered {status.Code} and closed.");

        if (await TryWriteRefusalAsync(status, allow))
        {
            await unreadRequestDrainer.DrainAsync();
        }

        return false;
    }

    private async Task<bool> TryWriteRefusalAsync(HttpStatus status, string? allow)
    {
        using var deadline = new CancellationTokenSource(RefusalWriteDeadline, context.TimeProvider);
        using var deadlineOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            await connection.WriteAsync(EmptyResponseHead(status, allow, false, HttpVersion11).ToBytes(), deadlineOrExchange.Token);
            await connection.CompleteWritesAsync(deadlineOrExchange.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note($"The {status.Code} was not written within its {RefusalWriteDeadline.TotalSeconds}-second write deadline; the connection is closed without it.");

            return false;
        }

        return true;
    }

    private async Task<bool> WriteEmptyResponseAsync(HttpStatus status, string? allow, bool keepsConnectionOpen, Version requestVersion)
    {
        await connection.WriteAsync(EmptyResponseHead(status, allow, keepsConnectionOpen, requestVersion).ToBytes(), context.CancellationToken);

        return await FinishResponseAsync(keepsConnectionOpen);
    }

    private HttpResponseHead EmptyResponseHead(HttpStatus status, string? allow, bool keepsConnectionOpen, Version requestVersion) =>
        AddWwwAuthenticateFields(new HttpResponseHead(status)
            .AddField("Date", FormatHttpDate(Now()))
            .AddField("Server", HttpResponseHead.ServerName)
            .AddField("Allow", allow)
            .AddField("Content-Length", "0"))
            .AddField("Connection", HttpConnectionPersistence.ConnectionFieldValue(keepsConnectionOpen, requestVersion));

    // One WWW-Authenticate field per value, in order (ADR-0032, section 6).
    private HttpResponseHead AddWwwAuthenticateFields(HttpResponseHead responseHead)
    {
        foreach (var value in wwwAuthenticateValues)
        {
            responseHead.AddField("WWW-Authenticate", value);
        }

        return responseHead;
    }

    private async Task<bool> WriteFileResponseAsync(HttpRequestHead head, ContentPathMapping mapping, ContentFileStatus status, bool keepsConnectionOpen)
    {
        var now = Now();
        var lastModified = status.LastModifiedUtc < now ? status.LastModifiedUtc : now;
        var responseHead = AddWwwAuthenticateFields(new HttpResponseHead(HttpStatus.Ok)
            .AddField("Date", FormatHttpDate(now))
            .AddField("Server", HttpResponseHead.ServerName)
            .AddField("Last-Modified", FormatHttpDate(lastModified))
            .AddField("Content-Type", "application/octet-stream")
            .AddField("Content-Length", status.Length.ToString(CultureInfo.InvariantCulture)))
            .AddField("Connection", HttpConnectionPersistence.ConnectionFieldValue(keepsConnectionOpen, head.Version));

        context.Log.Note($"{head.Method} {head.RequestTarget}: 200, {status.Length} bytes of {mapping.Location}");
        await connection.WriteAsync(responseHead.ToBytes(), context.CancellationToken);

        if (head.Method == "GET" && !await CopyWholeFileAsync(mapping, status.Length))
        {
            connection.Abort();

            return false;
        }

        return await FinishResponseAsync(keepsConnectionOpen);
    }

    // After the 200 head the Content-Length is promised, so a file that shrinks or cannot be
    // read any more can only be answered by aborting the connection; a reset tells curl the
    // transfer failed. A failure of the connection itself is not the file's and is rethrown.
    private async Task<bool> CopyWholeFileAsync(ContentPathMapping mapping, long length)
    {
        await using var body = new ConnectionWriteStream(connection);
        try
        {
            var copied = await contentStore.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(length), body, context.CancellationToken);
            if (copied < length)
            {
                context.Log.Note($"{mapping.Location} shrank to {copied} bytes while it was sent; the connection was aborted.");
            }

            return copied == length;
        }
        catch (Exception failure) when (IsFileSystemFailure(failure) && !body.HasFailedWrite)
        {
            context.Log.Note($"{mapping.Location} could not be read after the 200 head was sent ({failure.GetType().Name}: {failure.Message}); the connection was aborted.");

            return false;
        }
    }

    private async Task<bool> FinishResponseAsync(bool keepsConnectionOpen)
    {
        if (!keepsConnectionOpen)
        {
            await connection.CompleteWritesAsync(context.CancellationToken);
            await unreadRequestDrainer.DrainAsync();
        }

        return keepsConnectionOpen;
    }

    private DateTimeOffset Now() => context.TimeProvider.GetUtcNow();

    // IMF-fixdate (RFC 9110, section 5.6.7): "r" is exactly that format, in UTC.
    private static string FormatHttpDate(DateTimeOffset time) => time.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
}
