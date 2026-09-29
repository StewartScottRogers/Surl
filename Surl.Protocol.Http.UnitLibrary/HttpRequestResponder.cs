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
    private const string AllowedMethods = "GET, HEAD";

    private const string AbsoluteFormPrefix = "http://";

    private static readonly Version HttpVersion11 = new(1, 1);

    private static readonly string[] MethodsRefusedWithAllow = ["POST", "PUT", "DELETE", "CONNECT", "OPTIONS", "TRACE", "PATCH"];

    // Every other failure to read a head is a malformed head: 400.
    private static readonly Dictionary<HttpRequestHeadReadOutcome, HttpStatus> StatusesForHeadsNotRead = new()
    {
        [HttpRequestHeadReadOutcome.UnsupportedVersion] = HttpStatus.HttpVersionNotSupported,
        [HttpRequestHeadReadOutcome.HeadTooLarge] = HttpStatus.RequestHeaderFieldsTooLarge,
    };

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly ContentStore contentStore;

    /// <summary>
    /// Creates a responder for one connection.
    /// </summary>
    /// <param name="connection">Where responses are written.</param>
    /// <param name="context">The exchange: its clock, log and cancellation.</param>
    /// <param name="contentStore">Where request paths are looked up.</param>
    public HttpRequestResponder(IConnection connection, ExchangeContext context, ContentStore contentStore)
    {
        this.connection = connection;
        this.context = context;
        this.contentStore = contentStore;
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

        return head.Method is "GET" or "HEAD" ? AnswerFromContentStoreAsync(head) : RefuseMethodAsync(head);
    }

    /// <summary>
    /// Answers a connection on which no request head could be read: nothing when the client
    /// closed it, otherwise a refusal that closes it.
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

        var status = StatusesForHeadsNotRead.GetValueOrDefault(outcome, HttpStatus.BadRequest);

        return RefuseAsync(status, null, $"No request head was read: {outcome}");
    }

    private async Task<bool> AnswerFromContentStoreAsync(HttpRequestHead head)
    {
        var keepsConnectionOpen = HttpConnectionPersistence.KeepsConnectionOpen(head);
        var mapping = contentStore.MapRequestPath(RequestPath(head.RequestTarget));
        var status = mapping.IsMapped ? contentStore.GetFileStatus(mapping) : null;
        if (status is null)
        {
            context.Log.Note($"{head.Method} {head.RequestTarget}: 404, {WhyNotFound(mapping)}");

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

    private static string WhyNotFound(ContentPathMapping mapping) => mapping switch
    {
        { IsMapped: false } => $"the path was refused ({mapping.Refusal})",
        { EntryKind: ContentEntryKind.Directory } => $"{mapping.Location} is a directory",
        _ => $"nothing exists at {mapping.Location}",
    };

    private Task<bool> RefuseMethodAsync(HttpRequestHead head)
    {
        var isKnown = MethodsRefusedWithAllow.Contains(head.Method, StringComparer.Ordinal);
        var status = isKnown ? HttpStatus.MethodNotAllowed : HttpStatus.NotImplemented;

        return RefuseAsync(status, isKnown ? AllowedMethods : null, $"{head.Method} {head.RequestTarget}: the method is not served");
    }

    private async Task<bool> RefuseAsync(HttpStatus status, string? allow, string note)
    {
        context.Log.Note($"{note}; answered {status.Code} and closed.");

        return await WriteEmptyResponseAsync(status, allow, false, HttpVersion11);
    }

    private async Task<bool> WriteEmptyResponseAsync(HttpStatus status, string? allow, bool keepsConnectionOpen, Version requestVersion)
    {
        var responseHead = new HttpResponseHead(status)
            .AddField("Date", FormatHttpDate(Now()))
            .AddField("Allow", allow)
            .AddField("Content-Length", "0")
            .AddField("Connection", HttpConnectionPersistence.ConnectionFieldValue(keepsConnectionOpen, requestVersion));

        await connection.WriteAsync(responseHead.ToBytes(), context.CancellationToken);

        return await FinishResponseAsync(keepsConnectionOpen);
    }

    private async Task<bool> WriteFileResponseAsync(HttpRequestHead head, ContentPathMapping mapping, ContentFileStatus status, bool keepsConnectionOpen)
    {
        var now = Now();
        var lastModified = status.LastModifiedUtc < now ? status.LastModifiedUtc : now;
        var responseHead = new HttpResponseHead(HttpStatus.Ok)
            .AddField("Date", FormatHttpDate(now))
            .AddField("Last-Modified", FormatHttpDate(lastModified))
            .AddField("Content-Type", "application/octet-stream")
            .AddField("Content-Length", status.Length.ToString(CultureInfo.InvariantCulture))
            .AddField("Connection", HttpConnectionPersistence.ConnectionFieldValue(keepsConnectionOpen, head.Version));

        context.Log.Note($"{head.Method} {head.RequestTarget}: 200, {status.Length} bytes of {mapping.Location}");
        await connection.WriteAsync(responseHead.ToBytes(), context.CancellationToken);

        if (head.Method == "GET")
        {
            await using var body = new ConnectionWriteStream(connection);
            var copied = await contentStore.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(status.Length), body, context.CancellationToken);
            if (copied < status.Length)
            {
                context.Log.Note($"{mapping.Location} shrank to {copied} bytes while it was sent; the connection was aborted.");
                connection.Abort();

                return false;
            }
        }

        return await FinishResponseAsync(keepsConnectionOpen);
    }

    private async Task<bool> FinishResponseAsync(bool keepsConnectionOpen)
    {
        if (!keepsConnectionOpen)
        {
            await connection.CompleteWritesAsync(context.CancellationToken);
        }

        return keepsConnectionOpen;
    }

    private DateTimeOffset Now() => context.TimeProvider.GetUtcNow();

    // IMF-fixdate (RFC 9110, section 5.6.7): "r" is exactly that format, in UTC.
    private static string FormatHttpDate(DateTimeOffset time) => time.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
}
