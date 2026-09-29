using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The HTTP server: answers <c>GET</c> and <c>HEAD</c> for files in the content store over
/// HTTP/1.0 and HTTP/1.1, one request after another on a persistent connection (RFC 9110,
/// RFC 9112). ADR-0008 records the answers below.
/// </summary>
/// <remarks>
/// <para>
/// A file is answered <c>200 OK</c> with <c>Date</c>, <c>Last-Modified</c>,
/// <c>Content-Type: application/octet-stream</c> (until the content store knows media
/// types, RFC 9110 section 8.3) and <c>Content-Length</c>, then the file's bytes for
/// <c>GET</c> and none for <c>HEAD</c>. The status line always names HTTP/1.1.
/// </para>
/// <para>
/// <c>404 Not Found</c>, with an empty body, answers a path the content store refuses
/// (such as <c>/%2e%2e/x</c>), a path where nothing exists, and a directory (listing is a
/// later task, and a refused listing is answered as absent, ADR-0006 section 2). A 404
/// leaves the connection as the request's version and <c>Connection</c> field decide.
/// </para>
/// <para>
/// A method RFC 9110 defines that the content store refuses (<c>POST</c>, <c>PUT</c>,
/// <c>DELETE</c>, <c>CONNECT</c>, <c>OPTIONS</c>, <c>TRACE</c>, and <c>PATCH</c> from
/// RFC 5789) is answered <c>405 Method Not Allowed</c> with <c>Allow: GET, HEAD</c>; any
/// other method <c>501 Not Implemented</c>. A malformed head, and an HTTP/1.1 request
/// without exactly one <c>Host</c> field, is answered <c>400 Bad Request</c>; a head past
/// the size limit <c>431 Request Header Fields Too Large</c>; a major version other than 1
/// <c>505 HTTP Version Not Supported</c>. Each of these says <c>Connection: close</c>, has
/// an empty body, and the connection is half-closed after it, because the request's body,
/// if any, has not been read.
/// </para>
/// <para>
/// A connection closes after a response when the request said <c>Connection: close</c>,
/// was HTTP/1.0 without <c>Connection: keep-alive</c>, or announced a body; otherwise the
/// next request is read from it. A client that closes before a whole head arrives gets no
/// bytes. If a file shrinks while it is sent, the connection is aborted, because its
/// <c>Content-Length</c> can no longer be met.
/// </para>
/// </remarks>
public sealed class HttpProtocolServer : IConnectionProtocolServer
{
    private readonly ContentStore contentStore;

    /// <summary>
    /// Creates an HTTP server that serves <paramref name="contentStore"/>.
    /// </summary>
    /// <param name="contentStore">The content store every request path is looked up in.</param>
    public HttpProtocolServer(ContentStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);

        this.contentStore = contentStore;
    }

    /// <summary>
    /// The one scheme answered: <c>http</c>. <c>https</c> joins it with the TLS contract.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["http"]);

    /// <summary>
    /// Answers every request on <paramref name="connection"/> until the client closes it or
    /// a response closes it.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var reader = new HttpConnectionReader(connection);
        var responder = new HttpRequestResponder(connection, context, contentStore);
        var keepsConnectionOpen = true;

        while (keepsConnectionOpen)
        {
            var result = await reader.ReadRequestHeadAsync(context.CancellationToken);
            keepsConnectionOpen = result.Head is { } head
                ? await responder.AnswerRequestAsync(head)
                : await responder.AnswerHeadNotReadAsync(result.Outcome);
        }
    }
}
