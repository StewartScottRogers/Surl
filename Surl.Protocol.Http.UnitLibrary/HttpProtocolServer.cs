using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The HTTP server: answers <c>GET</c> and <c>HEAD</c> for files in the content store over
/// HTTP/1.0 and HTTP/1.1, one request after another on a persistent connection (RFC 9110,
/// RFC 9112), within the exchange's hardening limits (ADR-0006). ADR-0008 and ADR-0019
/// record the answers below.
/// </summary>
/// <remarks>
/// <para>
/// Every response names the server <c>Server: surl</c>, with no version, and every error
/// response has an empty body and the reason phrase from the server's own status table, so
/// no path, exception text or host detail reaches a client (ADR-0006, section 3).
/// </para>
/// <para>
/// A file is answered <c>200 OK</c> with <c>Date</c>, <c>Server</c>, <c>Last-Modified</c>,
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
/// <see cref="ExchangeLimits.MaxRequestHeadBytes"/> <c>431 Request Header Fields Too
/// Large</c>, without reading more of it; a major version other than 1 <c>505 HTTP Version
/// Not Supported</c>. Each of these refusals says <c>Connection: close</c>, has an empty
/// body, gets one second to be written, and is followed by a half-close; a refusal that
/// misses the second is given up and the connection closed without it, never reset.
/// </para>
/// <para>
/// The head timeout (<see cref="ExchangeLimits.HeadTimeout"/>, on the exchange's
/// <see cref="ExchangeContext.TimeProvider"/>) bounds the first head from the moment
/// <see cref="ServeAsync"/> starts, and each later head from its first byte; the wait for
/// that first byte on a kept-alive connection is left to the engine's idle timeout. When it
/// runs out part way through a head the answer is <c>408 Request Timeout</c> as a refusal;
/// when no byte of the head has arrived, the connection closes with no bytes.
/// </para>
/// <para>
/// Request bodies: the upload limit (<see cref="ExchangeLimits.MaxUploadBytes"/>) is
/// checked after the <c>Host</c> check and before method dispatch, so any method whose
/// <c>Content-Length</c> is past it is answered <c>413 Content Too Large</c> as a refusal
/// before any body byte is read; a request with <c>Expect: 100-continue</c> gets that 413
/// in place of <c>100 Continue</c>. After dispatch, the body of a <c>GET</c> or
/// <c>HEAD</c> is read and discarded before the answer, after <c>HTTP/1.1 100 Continue</c>
/// when an HTTP/1.1 request carries <c>Expect: 100-continue</c> (ADR-0027): a chunked one is answered 413 as
/// soon as its chunk data and trailer lines pass the limit, and one that is malformed or
/// ends early <c>400 Bad Request</c>. A body the server cannot frame (a transfer coding
/// other than <c>chunked</c>) is not read, and the connection closes after the response.
/// Refused methods never read the body to answer. Without <c>Transfer-Encoding</c>, a
/// <c>Content-Length</c> that is not one field of decimal digits is answered
/// <c>400 Bad Request</c> as a refusal, before method dispatch (RFC 9112, section 6.3; ADR-0024).
/// </para>
/// <para>
/// Lingering close (ADR-0024): after any response that half-closes the connection, the
/// server reads and discards what the client still sends until the client half-closes, for
/// at most one second on the exchange's clock and at most 1 MiB, so the unread bytes of a
/// refused request do not turn the close into a TCP reset that destroys the response.
/// </para>
/// <para>
/// A connection closes after a response when the request said <c>Connection: close</c>,
/// was HTTP/1.0 without <c>Connection: keep-alive</c>, or had a body the server cannot
/// frame; otherwise the next request is read from it. A client that closes before a whole
/// head arrives gets no bytes. If a file shrinks while it is sent, the connection is
/// aborted, because its <c>Content-Length</c> can no longer be met.
/// </para>
/// <para>
/// File-system failures (ADR-0023): a file whose status the content store cannot read
/// (<see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>) is answered
/// <c>404 Not Found</c>, exactly as a missing file, and the reason goes to the exchange log
/// only. A file that cannot be opened or read after the <c>200</c> head was sent aborts the
/// connection with a log note naming the file.
/// </para>
/// <para>
/// As an <see cref="IConnectionRefusalWriter"/>, the server answers a connection past a
/// connection limit with <c>503 Service Unavailable</c>, <c>Server: surl</c>,
/// <c>Content-Length: 0</c> and <c>Connection: close</c>, then half-closes it; it carries no
/// <c>Date</c>, which RFC 9110 section 6.6.1 leaves optional for a 5xx.
/// </para>
/// </remarks>
public sealed class HttpProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    private static readonly byte[] ServiceUnavailableResponse = new HttpResponseHead(HttpStatus.ServiceUnavailable)
        .AddField("Server", HttpResponseHead.ServerName)
        .AddField("Content-Length", "0")
        .AddField("Connection", "close")
        .ToBytes();

    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1.0);

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
    /// The one scheme answered: <c>http</c>. <c>surl</c> serves <c>https</c> with this same
    /// server, registered through <c>Surl.Console</c>'s <c>ImplicitTlsSchemeServer</c> on
    /// connections the serving engine has already secured with TLS (ADR-0020).
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

        var reader = new HttpConnectionReader(connection, context.Limits.MaxRequestHeadBytes);
        var responder = new HttpRequestResponder(connection, reader, context, contentStore);
        var isFirstHead = true;
        var keepsConnectionOpen = true;

        while (keepsConnectionOpen)
        {
            var result = await ReadNextHeadAsync(reader, context, isFirstHead);
            isFirstHead = false;
            keepsConnectionOpen = result.Head is { } head
                ? await responder.AnswerRequestAsync(head)
                : await responder.AnswerHeadNotReadAsync(result.Outcome);
        }
    }

    /// <summary>
    /// Writes <c>503 Service Unavailable</c> with <c>Connection: close</c> for either
    /// refusal, then half-closes the connection. The engine bounds the write with its own
    /// deadline through <paramref name="cancellationToken"/>.
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

    // A timer cannot wait longer than uint.MaxValue - 1 milliseconds (about 49.7 days); a
    // head timeout past that never fires in practice, so it is treated as none.
    private static TimeSpan TimerDelay(TimeSpan headTimeout) =>
        headTimeout > MaxTimerDelay ? Timeout.InfiniteTimeSpan : headTimeout;

    // The first head is timed from now; a later one from its first byte, and a client that
    // closes before sending it simply ends the connection.
    private static async Task<HttpRequestHeadReadResult> ReadNextHeadAsync(HttpConnectionReader reader, ExchangeContext context, bool isFirstHead)
    {
        if (!isFirstHead && !await reader.WaitForBytesAsync(context.CancellationToken))
        {
            return HttpRequestHeadReadResult.NoHead(HttpRequestHeadReadOutcome.ConnectionClosed);
        }

        using var headTimeout = new CancellationTokenSource(TimerDelay(context.Limits.HeadTimeout), context.TimeProvider);
        using var headTimeoutOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await reader.ReadRequestHeadAsync(headTimeoutOrExchange.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return HttpRequestHeadReadResult.NoHead(reader.HasReceivedHeadBytes
                ? HttpRequestHeadReadOutcome.HeadTimedOut
                : HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte);
        }
    }
}
