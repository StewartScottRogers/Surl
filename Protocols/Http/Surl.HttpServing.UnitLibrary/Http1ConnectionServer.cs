using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.HttpServing;

/// <summary>
/// Serves one HTTP/1.x connection for an <see cref="IHttpRequestHandler"/>: reads each request
/// head, answers it through the handler, writes the answer, and keeps or closes the connection
/// (RFC 9112; ADR-0084, decision 1).
/// </summary>
/// <remarks>
/// <para>
/// Each head is read through <see cref="HttpConnectionReader"/>, bounded by
/// <see cref="ExchangeLimits.MaxRequestHeadBytes"/> and the head timeout. A head that cannot be
/// read is answered with no bytes when the client closed or sent nothing, and otherwise refused:
/// <c>505</c> for a major version other than 1, <c>431</c> for a head past the limit,
/// <c>408</c> for one the head timeout cut off, <c>400</c> for any other. An HTTP/1.1 request
/// without exactly one <c>Host</c> field (RFC 9112, section 3.2), any request with two, and a
/// <c>Content-Length</c> that is not one field of decimal digits (RFC 9112, section 6.3) are
/// refused <c>400</c> before the handler sees them.
/// </para>
/// <para>
/// A refusal says <c>Connection: close</c>, has one second to be written, and is followed by a
/// half-close. The request body is read only when the handler reads it: after
/// <c>100 Continue</c> when an HTTP/1.1 request expects one (RFC 9110, section 10.1.1), with
/// <c>Content-Length</c> or chunked framing (RFC 9112, sections 6 and 7), counted against
/// <see cref="ExchangeLimits.MaxUploadBytes"/>. The connection stays open after an answer when
/// it is no refusal, left no announced body unread, and the request's version and
/// <c>Connection</c> field keep it open (RFC 9112, section 9.3); otherwise it is half-closed and
/// what the client still sends is drained for at most one second and 1 MiB (RFC 9112,
/// section 9.6; ADR-0024). An answer with an <see cref="HttpAnswer.Takeover"/> hands the
/// connection over after its head.
/// </para>
/// </remarks>
public sealed class Http1ConnectionServer
{
    private readonly IHttpRequestHandler handler;
    private readonly ExchangeContext context;

    /// <summary>
    /// Creates a server for one connection of the exchange <paramref name="context"/> names.
    /// </summary>
    /// <param name="handler">Decides the answer to every well-formed request.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    public Http1ConnectionServer(IHttpRequestHandler handler, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(context);

        this.handler = handler;
        this.context = context;
    }

    /// <summary>
    /// Answers every request on <paramref name="connection"/> until the client closes it, an
    /// answer closes it, or a takeover ends.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var reader = new HttpConnectionReader(connection, context.Limits.MaxRequestHeadBytes);
        var responder = new Http1RequestResponder(connection, reader, context, handler);
        var isFirstHead = true;
        var keepsConnectionOpen = true;

        while (keepsConnectionOpen)
        {
            var result = await reader.ReadNextRequestHeadAsync(context, isFirstHead);
            isFirstHead = false;
            keepsConnectionOpen = result.Head is { } head
                ? await responder.AnswerRequestAsync(head)
                : await responder.AnswerHeadNotReadAsync(result.Outcome);
        }
    }
}
