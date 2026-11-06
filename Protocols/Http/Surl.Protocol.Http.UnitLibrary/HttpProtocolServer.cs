using System.Net;
using Surl.Content;
using Surl.Hpack;
using Surl.HttpMessage;
using Surl.HttpServing;
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
/// Authentication (ADR-0032, sections 4 and 6): each connection gets one
/// <see cref="IHttpAuthenticationSession"/> from the <see cref="IAuthenticationPolicy"/>,
/// started with the connection's <see cref="IConnection.TlsSession"/> before its first
/// request, so a connection-bound handshake (NTLM, Negotiate) lives and dies with it. Every
/// request whose head passes the <c>Host</c> check and has a well-formed
/// <c>Content-Length</c> is judged, before the upload limit, before <c>100 Continue</c>,
/// before any body byte is read and before method dispatch. <c>Challenge</c> is answered
/// <c>401 Unauthorized</c> with one <c>WWW-Authenticate</c> field per value the session gave,
/// after <c>Content-Length</c>; it keeps the connection as a <c>404</c> does, unless the
/// request announced a body, when it is a refusal. <c>Forbidden</c> is answered
/// <c>403 Forbidden</c> as a refusal. <c>Proceed</c> goes on to the answers below, and any
/// values it carries are written on that answer the same way. A <c>Proceed</c> carrying a
/// <see cref="HttpAuthenticationVerdict.BodyCheck"/> - a login bound to the body, AWS Signature
/// Version 4 (ADR-0045) - is held until the body is read: the upload limit is checked, the body
/// is read (after <c>100 Continue</c> when expected) and hashed with SHA-256, and the request is
/// answered as the body check's verdict says, as one with no body left to read. A body that
/// ends early is <c>400</c>, and one the server cannot frame is <c>400</c> unread; both are
/// refusals. Neither the log nor any response repeats the request's <c>Authorization</c>.
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
/// HTTP/2 with prior knowledge (ADR-0081, decision 3; ADR-0090): on a connection that is not
/// secured with TLS, the first bytes are read within the head timeout until they stop matching
/// the HTTP/2 connection preface. The whole preface hands the connection to
/// <see cref="Http2ConnectionServer"/>, which answers each request stream through the same
/// answerer; anything else is read as an HTTP/1.x request, the bytes already read first, and
/// answered exactly as above. A head timeout that runs out with part of the preface read closes
/// the connection with no bytes.
/// </para>
/// <para>
/// HTTP/2 by <c>Upgrade: h2c</c> (ADR-0081, decision 3; ADR-0091): on a cleartext connection, an
/// HTTP/1.1 request with no body that names <c>h2c</c> in <c>Upgrade</c>, carries one
/// <c>HTTP2-Settings</c> that decodes, and names both in <c>Connection</c>
/// (<see cref="Http2CleartextUpgrade"/>) is answered <c>HTTP/1.1 101 Switching Protocols</c>,
/// <c>Connection: Upgrade</c>, <c>Upgrade: h2c</c>, and the connection becomes HTTP/2 with that
/// request answered on stream 1 once the client's preface arrives. Any other request, and every
/// request over TLS, ignores the upgrade and is answered HTTP/1.1 as above.
/// </para>
/// <para>
/// As an <see cref="IConnectionRefusalWriter"/>, the server answers a connection past a
/// connection limit with <c>503 Service Unavailable</c>, <c>Server: surl</c>,
/// <c>Content-Length: 0</c> and <c>Connection: close</c>, then half-closes it; it carries no
/// <c>Date</c>, which RFC 9110 section 6.6.1 leaves optional for a 5xx. It first reads the
/// client's first bytes within the engine's refusal deadline: a client that opens with the HTTP/2
/// preface gets the same <c>503</c> as HTTP/2 on its first stream - an empty <c>SETTINGS</c>,
/// <c>HEADERS</c> <c>:status 503</c> with <c>END_STREAM</c>, and <c>GOAWAY</c> naming that stream,
/// <c>NO_ERROR</c> (ADR-0081, decision 5) - and one that sends nothing, or no <c>HEADERS</c>
/// after the preface, before the deadline gets no bytes.
/// </para>
/// </remarks>
public sealed class HttpProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    private static readonly byte[] ServiceUnavailableResponse = new HttpResponseHead(HttpStatus.ServiceUnavailable)
        .AddField("Server", HttpResponseHead.ServerName)
        .AddField("Content-Length", "0")
        .AddField("Connection", "close")
        .ToBytes();

    // The same 503 as one HTTP/2 header block (ADR-0081, decision 5): the fields above but
    // Connection, which HTTP/2 forbids (RFC 9113 section 8.2.2).
    private static readonly HpackHeaderField[] Http2ServiceUnavailableFields =
    [
        new(":status", "503"),
        new("server", HttpResponseHead.ServerName),
        new("content-length", "0"),
    ];

    // The ALPN protocol ID of HTTP/2 over TLS (RFC 9113, section 3.2). A TLS connection that
    // agreed it is read for the connection preface, as a cleartext one is; one that agreed
    // http/1.1, or no ALPN at all, is served HTTP/1.x (ADR-0081, decision 2).
    private const string Http2OverTlsApplicationProtocol = "h2";

    private readonly ContentStore contentStore;

    private readonly IAuthenticationPolicy authenticationPolicy;

    private readonly bool advertisesHttp3;

    /// <summary>
    /// Creates an HTTP server that serves <paramref name="contentStore"/> to the requests
    /// <paramref name="authenticationPolicy"/> lets in (ADR-0032, sections 4 and 6).
    /// </summary>
    /// <param name="contentStore">The content store every request path is looked up in.</param>
    /// <param name="authenticationPolicy">
    /// Judges every request: served, challenged with <c>401</c>, or refused with <c>403</c>.
    /// </param>
    public HttpProtocolServer(ContentStore contentStore, IAuthenticationPolicy authenticationPolicy)
        : this(contentStore, authenticationPolicy, advertisesHttp3: false)
    {
    }

    /// <summary>
    /// Creates an HTTP server that serves <paramref name="contentStore"/> to the requests
    /// <paramref name="authenticationPolicy"/> lets in, and, when
    /// <paramref name="advertisesHttp3"/>, adds <c>Alt-Svc: h3=":&lt;port&gt;"; ma=86400</c> to every
    /// answer on a TLS connection, the port being the connection's own (ADR-0082, decision 6).
    /// </summary>
    /// <param name="contentStore">The content store every request path is looked up in.</param>
    /// <param name="authenticationPolicy">
    /// Judges every request: served, challenged with <c>401</c>, or refused with <c>403</c>.
    /// </param>
    /// <param name="advertisesHttp3">Whether HTTP/3 is served on the port of every TLS connection (<c>--http3</c>).</param>
    public HttpProtocolServer(ContentStore contentStore, IAuthenticationPolicy authenticationPolicy, bool advertisesHttp3)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.contentStore = contentStore;
        this.authenticationPolicy = authenticationPolicy;
        this.advertisesHttp3 = advertisesHttp3;
    }

    /// <summary>
    /// The one scheme answered: <c>http</c>. <c>surl</c> serves <c>https</c> with this same
    /// server, registered through <c>Surl.Composition</c>'s <c>ImplicitTlsSchemeServer</c> on
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

        var authenticationSession = authenticationPolicy.StartHttpConnection(connection.TlsSession);
        var answerer = AdvertiseHttp3(new HttpRequestAnswerer(context, contentStore, authenticationSession), connection);
        if (connection.TlsSession is { ApplicationProtocol: not Http2OverTlsApplicationProtocol })
        {
            await new Http1ConnectionServer(answerer, context).ServeAsync(connection);

            return;
        }

        var preface = new Http2ConnectionPreface();
        if (!await ReadPrefaceWithinHeadTimeoutAsync(preface, connection, context))
        {
            return;
        }

        if (preface.IsComplete)
        {
            await new Http2ConnectionServer(new Http2StreamAnswerWriter(answerer, context), context, new AlreadyReadBytesConnection(connection, preface.BytesReadAfterPreface)).ServeAsync();

            return;
        }

        // Upgrade: h2c is cleartext only, so over TLS it is ignored (ADR-0081, decision 3).
        IHttpRequestHandler http1Handler = connection.TlsSession is null ? new Http2CleartextUpgradeHandler(answerer, context) : answerer;
        await new Http1ConnectionServer(http1Handler, context).ServeAsync(new AlreadyReadBytesConnection(connection, preface.BytesRead));
    }

    // With --http3, every answer on a TLS connection names HTTP/3 on the connection's own port;
    // a cleartext connection's, and every answer without --http3, are left as they are.
    private IHttpRequestHandler AdvertiseHttp3(HttpRequestAnswerer answerer, IConnection connection) =>
        advertisesHttp3 && connection is { TlsSession: not null, LocalEndPoint: IPEndPoint local }
            ? new AltSvcAdvertisingHandler(answerer, local.Port)
            : answerer;

    // The head timeout bounds the preface as it bounds a first request head: when it runs out
    // the connection closes with no bytes (ADR-0081, decision 5), as HTTP/1.x closes one that
    // sent no byte of its head. False when it ran out.
    private static async Task<bool> ReadPrefaceWithinHeadTimeoutAsync(Http2ConnectionPreface preface, IConnection connection, ExchangeContext context)
    {
        using var headTimeout = new CancellationTokenSource(HeadTimeoutDelay.For(context.Limits.HeadTimeout), context.TimeProvider);
        using var headTimeoutOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            await preface.ReadAsync(connection, headTimeoutOrExchange.Token);

            return true;
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note(preface.BytesRead.IsEmpty
                ? $"No request head was read: {HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte}; closed with no bytes."
                : $"The {preface.BytesRead.Length} bytes read are the start of the HTTP/2 connection preface, which was not complete within the head timeout; closed with no bytes.");

            return false;
        }
    }

    /// <summary>
    /// Answers either refusal with <c>503</c> in the client's own version, then half-closes the
    /// connection. The engine bounds the whole refusal, reading included, with its own deadline
    /// through <paramref name="cancellationToken"/>.
    /// </summary>
    /// <remarks>
    /// The first bytes are read with <see cref="Http2ConnectionPreface"/>. The whole HTTP/2
    /// preface is refused by <see cref="Http2ConnectionRefusal"/> on the client's first stream
    /// (ADR-0081, decision 5); anything else, a client that half-closes first included, gets the
    /// HTTP/1.1 <c>503</c>. A client that sends nothing before the deadline gets no bytes.
    /// </remarks>
    /// <param name="connection">The connection past the limit.</param>
    /// <param name="refusal">Which limit it is past; both are answered alike.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the refusal off.</exception>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var preface = new Http2ConnectionPreface();
        await preface.ReadAsync(connection, cancellationToken);
        if (preface.IsComplete)
        {
            await Http2ConnectionRefusal.WriteAsync(new AlreadyReadBytesConnection(connection, preface.BytesReadAfterPreface), Http2ServiceUnavailableFields, cancellationToken);

            return;
        }

        await connection.WriteAsync(ServiceUnavailableResponse, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
