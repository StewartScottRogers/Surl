using System.Globalization;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.HttpServing;

/// <summary>
/// Answers each request read from one HTTP/1.x connection: refuses a head RFC 9112 calls
/// malformed, has the <see cref="IHttpRequestHandler"/> decide the answer to every other, and
/// has <see cref="Http1AnswerWriter"/> write it, saying whether the connection stays open.
/// </summary>
internal sealed class Http1RequestResponder
{
    // Every other failure to read a head is a malformed head: 400.
    private static readonly Dictionary<HttpRequestHeadReadOutcome, HttpStatus> StatusesForHeadsNotRead = new()
    {
        [HttpRequestHeadReadOutcome.UnsupportedVersion] = HttpStatus.HttpVersionNotSupported,
        [HttpRequestHeadReadOutcome.HeadTooLarge] = HttpStatus.RequestHeaderFieldsTooLarge,
        [HttpRequestHeadReadOutcome.HeadTimedOut] = HttpStatus.RequestTimeout,
    };

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly HttpRequestBodyDiscarder bodyDiscarder;
    private readonly IHttpRequestHandler handler;
    private readonly Http1AnswerWriter writer;

    /// <summary>
    /// Creates a responder for one connection.
    /// </summary>
    /// <param name="connection">Where responses are written.</param>
    /// <param name="reader">The connection's reader, through which request bodies are read.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="handler">Decides the answer to every well-formed request.</param>
    public Http1RequestResponder(IConnection connection, HttpConnectionReader reader, ExchangeContext context, IHttpRequestHandler handler)
    {
        this.connection = connection;
        this.context = context;
        this.handler = handler;
        bodyDiscarder = new HttpRequestBodyDiscarder(reader, context.Limits.MaxUploadBytes);
        writer = new Http1AnswerWriter(connection, new HttpUnreadRequestDrainer(reader, context), context);
    }

    /// <summary>
    /// Answers a request whose head was read.
    /// </summary>
    /// <param name="head">The request head.</param>
    /// <returns><see langword="true"/> when the connection stays open for another request.</returns>
    public async Task<bool> AnswerRequestAsync(HttpRequestHead head)
    {
        var framing = HttpRequestBodyFraming.Of(head);
        var answer = RefuseMalformedHead(head, framing)
            ?? await handler.AnswerAsync(Request(head, framing), context.CancellationToken);

        return await writer.WriteAsync(answer, head);
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

        return writer.WriteRefusalAsync(Refuse(status, $"No request head was read: {outcome}"));
    }

    // The request as the handler sees it, whatever version carried it. The authority is the
    // one Host field, which a well-formed HTTP/1.0 request may leave out.
    private HttpRequest Request(HttpRequestHead head, HttpRequestBodyFraming framing)
    {
        var hosts = head.GetFieldValues("Host");

        return new HttpRequest(
            head.Method,
            head.RequestTarget,
            hosts.Count == 1 ? hosts[0] : null,
            head.Fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)).ToArray(),
            new Http1RequestBody(connection, head, framing, bodyDiscarder));
    }

    // A head that is not well-formed - a missing or repeated Host (RFC 9112, section 3.2), a
    // Content-Length that is not one field of decimal digits (ADR-0024) - is refused before
    // the handler sees it (ADR-0032, section 4); null when the head is well-formed.
    private HttpAnswer? RefuseMalformedHead(HttpRequestHead head, HttpRequestBodyFraming framing)
    {
        if (!HasHostAsRequired(head))
        {
            return Refuse(HttpStatus.BadRequest, $"{head.Method} {head.RequestTarget}: a request may carry at most one Host field, and an HTTP/1.1 request needs one");
        }

        return framing.Kind == HttpRequestBodyFramingKind.InvalidContentLength
            ? Refuse(HttpStatus.BadRequest, $"{head.Method} {head.RequestTarget}: the Content-Length is not one field of decimal digits")
            : null;
    }

    // RFC 9112, section 3.2: 400 for an HTTP/1.1 request without Host, and for any request with two.
    private static bool HasHostAsRequired(HttpRequestHead head)
    {
        var hostCount = head.GetFieldValues("Host").Count;

        return hostCount == 1 || (hostCount == 0 && head.Version.Minor == 0);
    }

    // A refusal the handler never sees: Date (IMF-fixdate, RFC 9110 section 5.6.7), Server and
    // an empty body, logged with why (ADR-0006, section 3).
    private HttpAnswer Refuse(HttpStatus status, string note)
    {
        context.Log.Note($"{note}; answered {status.Code} and closed.");
        KeyValuePair<string, string>[] fields =
        [
            new("Date", context.TimeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture)),
            new("Server", HttpResponseHead.ServerName),
            new("Content-Length", "0"),
        ];

        return new HttpAnswer(status, fields, HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: false);
    }
}
