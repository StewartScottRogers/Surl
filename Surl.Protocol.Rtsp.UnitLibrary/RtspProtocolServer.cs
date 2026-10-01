using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The RTSP server (RFC 2326): reads RTSP/1.0 requests one after another on a persistent
/// connection with <c>Surl.HttpMessage</c> (ADR-0070), and answers them as ADR-0074 decides.
/// </summary>
/// <remarks>
/// <para>
/// Every response is <c>RTSP/1.0 &lt;status&gt; &lt;reason&gt;</c>, then the request's
/// <c>CSeq</c> copied byte for byte, <c>Date</c> from the exchange's
/// <see cref="ExchangeContext.TimeProvider"/> and <c>Server: surl</c> (ADR-0074 decision 1).
/// Only <c>DESCRIBE</c>'s answer has a body and a <c>Content-Length</c>; every other response,
/// refusals included, has neither.
/// </para>
/// <para>
/// <c>OPTIONS</c> - the only request the <c>curl</c> tool sends, always <c>OPTIONS *</c> - is
/// answered <c>200 OK</c> with <c>Public</c> naming all ten methods, whatever path it names.
/// <c>DESCRIBE</c> of an absolute <c>rtsp://</c> URL whose path names a file in the content
/// store is answered <c>200 OK</c> with <c>Content-Type: application/sdp</c>, <c>Content-Base</c>
/// (the Request-URI as sent) and the session description of one stream of the file's bytes
/// (ADR-0074 decision 4); a path that is refused, missing, hidden, a directory or unreadable is
/// <c>404 Not Found</c>, and <c>DESCRIBE *</c> is <c>400 Bad Request</c>.
/// </para>
/// <para>
/// Requests are judged in ADR-0074 decision 2's order, the first failing check answering. A head
/// that cannot be read is <c>400 Bad Request</c>, one past
/// <see cref="ExchangeLimits.MaxRequestHeadBytes"/> <c>431 Request Header Fields Too Large</c>,
/// one cut off by <see cref="ExchangeLimits.HeadTimeout"/> after its first byte
/// <c>408 Request Time-out</c>, and a version other than <c>RTSP/1.x</c>
/// <c>505 RTSP Version Not Supported</c>, each without <c>CSeq</c> and closing; a client that
/// sent no byte before the head timeout, or closed, gets no bytes. A request without exactly
/// one <c>CSeq</c> of 1 to 9 digits is <c>400 Bad Request</c> with no <c>CSeq</c>, keeping the
/// connection unless it announced a body. A <c>Transfer-Encoding</c> or a malformed
/// <c>Content-Length</c> is <c>400 Bad Request</c>, and a body past
/// <see cref="ExchangeLimits.MaxUploadBytes"/> <c>413 Request Entity Too Large</c>, both closing
/// with the body unread; any other body is read and discarded before the answer. A method that
/// is not one of RFC 2326's ten is <c>501 Not Implemented</c>, as is, until their tasks land,
/// every one of the ten but <c>OPTIONS</c> and <c>DESCRIBE</c>; a Request-URI that is neither
/// <c>*</c> nor an <c>rtsp://</c> URL is <c>400 Bad Request</c>. A closing refusal gets one
/// second to be written, is followed by a half-close, and what the client still sends is
/// drained (ADR-0019, ADR-0024). Each refusal is noted
/// <c>RTSP &lt;method&gt; refused: &lt;status&gt; &lt;reason&gt;: &lt;check&gt;</c>.
/// </para>
/// <para>
/// The idle timeout and the exchange's other limits end the exchange through its cancellation,
/// with no bytes. As an <see cref="IConnectionRefusalWriter"/>, the server answers a connection
/// past a connection limit with <c>RTSP/1.0 503 Service Unavailable</c> and <c>Server: surl</c>,
/// with no <c>CSeq</c> because no request was read, then half-closes it.
/// </para>
/// </remarks>
public sealed class RtspProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    private static readonly byte[] ServiceUnavailableResponse = new HttpResponseHead(HttpMessageProtocol.Rtsp10, RtspStatus.ServiceUnavailable)
        .AddField("Server", HttpResponseHead.ServerName)
        .ToBytes();

    private readonly ContentStore contentStore;

    /// <summary>
    /// Creates an RTSP server whose presentations are the files of <paramref name="contentStore"/>.
    /// </summary>
    /// <param name="contentStore">The content store every presentation path is looked up in.</param>
    public RtspProtocolServer(ContentStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);

        this.contentStore = contentStore;
    }

    /// <summary>
    /// The one scheme answered: <c>rtsp</c>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["rtsp"]);

    /// <summary>
    /// Answers every request on <paramref name="connection"/> until the client closes it or a
    /// refusal closes it.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var reader = new HttpConnectionReader(connection, context.Limits.MaxRequestHeadBytes, HttpMessageProtocol.Rtsp10);
        var responder = new RtspRequestResponder(connection, reader, context, contentStore);
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

    /// <summary>
    /// Writes <c>RTSP/1.0 503 Service Unavailable</c> for either refusal, then half-closes the
    /// connection (ADR-0074 decision 8). The engine bounds the write with its own deadline
    /// through <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="connection">The connection past the limit.</param>
    /// <param name="refusal">Which limit it is past; both are answered alike.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is written.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await connection.WriteAsync(ServiceUnavailableResponse, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
