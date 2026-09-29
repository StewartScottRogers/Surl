using System.Globalization;
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

    /// <summary>
    /// Creates a responder for one connection.
    /// </summary>
    /// <param name="connection">Where responses are written.</param>
    /// <param name="reader">The connection's reader, through which request bodies are read.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="contentStore">Where request paths are looked up.</param>
    public HttpRequestResponder(IConnection connection, HttpConnectionReader reader, ExchangeContext context, ContentStore contentStore)
    {
        this.connection = connection;
        this.context = context;
        this.contentStore = contentStore;
        bodyDiscarder = new HttpRequestBodyDiscarder(reader, context.Limits.MaxUploadBytes);
        unreadRequestDrainer = new HttpUnreadRequestDrainer(reader, context);
    }

    /// <summary>
    /// Answers a request whose head was read.
    /// </summary>
    /// <param name="head">The request head.</param>
    /// <returns><see langword="true"/> when the connection stays open for another request.</returns>
    public Task<bool> AnswerRequestAsync(HttpRequestHead head)
    {
        if (!HasHostAsRequired(head))
        {
            return RefuseAsync(HttpStatus.BadRequest, null, $"{head.Method} {head.RequestTarget}: a request may carry at most one Host field, and an HTTP/1.1 request needs one");
        }

        var framing = HttpRequestBodyFraming.Of(head);

        return RefuseFramingAsync(head, framing)
            ?? (head.Method is "GET" or "HEAD" ? AnswerFromContentStoreAsync(head, framing) : RefuseMethodAsync(head));
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
            : await bodyDiscarder.DiscardAsync(framing, context.CancellationToken);
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

    // Framing a request cannot have, whatever its method, is refused before method dispatch
    // (ADR-0024 for the 400, ADR-0019 for the 413); null when the framing is acceptable.
    private Task<bool>? RefuseFramingAsync(HttpRequestHead head, HttpRequestBodyFraming framing)
    {
        if (framing.Kind == HttpRequestBodyFramingKind.InvalidContentLength)
        {
            return RefuseAsync(HttpStatus.BadRequest, null, $"{head.Method} {head.RequestTarget}: the Content-Length is not one field of decimal digits");
        }

        return IsPastUploadLimit(framing)
            ? RefuseAsync(HttpStatus.ContentTooLarge, null, $"{head.Method} {head.RequestTarget}: the {framing.ContentLength}-byte body is past the upload limit of {context.Limits.MaxUploadBytes} bytes")
            : null;
    }

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
        new HttpResponseHead(status)
            .AddField("Date", FormatHttpDate(Now()))
            .AddField("Server", HttpResponseHead.ServerName)
            .AddField("Allow", allow)
            .AddField("Content-Length", "0")
            .AddField("Connection", HttpConnectionPersistence.ConnectionFieldValue(keepsConnectionOpen, requestVersion));

    private async Task<bool> WriteFileResponseAsync(HttpRequestHead head, ContentPathMapping mapping, ContentFileStatus status, bool keepsConnectionOpen)
    {
        var now = Now();
        var lastModified = status.LastModifiedUtc < now ? status.LastModifiedUtc : now;
        var responseHead = new HttpResponseHead(HttpStatus.Ok)
            .AddField("Date", FormatHttpDate(now))
            .AddField("Server", HttpResponseHead.ServerName)
            .AddField("Last-Modified", FormatHttpDate(lastModified))
            .AddField("Content-Type", "application/octet-stream")
            .AddField("Content-Length", status.Length.ToString(CultureInfo.InvariantCulture))
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
