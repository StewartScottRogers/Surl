using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The HTTP proxy server (ADR-0088 decisions 1, 3 and 4): the server curl's <c>-x</c>/<c>--proxy</c>
/// and <c>-p</c>/<c>--proxytunnel</c> talk to. It forwards absolute-form HTTP/1.1 requests to
/// their origin and tunnels <c>CONNECT</c> requests, reaching every origin through the
/// injected <see cref="IOriginConnector"/>.
/// </summary>
/// <remarks>
/// <para>
/// Request heads are read with <c>Surl.HttpMessage</c>'s reader within
/// <see cref="ExchangeLimits.MaxRequestHeadBytes"/> and <see cref="ExchangeLimits.HeadTimeout"/>,
/// one after another on curl's connection; a head that cannot be read is answered <c>431</c>,
/// <c>408</c>, <c>505</c> or <c>400</c> and closes the connection, and a request with more than
/// one <c>Host</c> is <c>400</c>.
/// </para>
/// <para>
/// <b>Proxy login.</b> Every request and every <c>CONNECT</c> is first judged by the proxy
/// authentication policy's session for curl's connection (ADR-0088 decision 5), given the
/// request's fields with <c>Authorization</c> removed and <c>Proxy-Authorization</c> read as
/// <c>Authorization</c>, and as its target a <c>CONNECT</c>'s authority or an absolute-form
/// request's path and query - what upstream curl puts in a Digest <c>uri</c>. A challenge is
/// answered <c>407 Proxy Authentication Required</c> with one <c>Proxy-Authenticate</c> per
/// value, keeping the connection (NTLM and Negotiate are bound to it) unless the request asked
/// to close or announced a body; a refusal is <c>403</c> and closes.
/// </para>
/// <para>
/// <b>Fault rules</b> (ADR-0160). A request or <c>CONNECT</c> the login let in, with a well formed
/// target, is put to the exchange's fault plan before its origin is contacted: a delay holds it, a
/// drop ends it with nothing sent, and a chosen error is the proxy's own answer with the rule's
/// status. A chosen <c>407</c>, whatever its reason phrase, carries one <c>Proxy-Authenticate</c>
/// per challenge the connection's proxy login session offers, over HTTP/2 too, NTLM and Negotiate
/// included (Amendment 2); a session offering none leaves it without the field, noted in the
/// exchange log. No other chosen status, <c>401</c> included, gains a challenge.
/// </para>
/// <para>
/// <b>Forwarding.</b> An absolute-form <c>http://</c> target is sent to its authority (port 80
/// when absent) in origin-form, with <c>Host</c> generated from the authority, the hop-by-hop
/// fields and <c>Proxy-Authorization</c> removed and <c>Via: 1.1 surl</c> added; the origin's
/// response comes back with its hop-by-hop fields, <c>Proxy-Authenticate</c> and
/// <c>Proxy-Authentication-Info</c> removed and <c>Via: 1.1 surl</c> added. A target that is not
/// absolute-form is <c>400</c>. An <c>ftp://</c> <c>GET</c> is fetched over FTP and answered
/// <c>200</c> with the file or listing, an FTP failure <c>502</c> and a timeout <c>504</c>
/// (Amendment 4); any other method to <c>ftp</c>, and any other scheme, is <c>501</c>. A <c>Content-Length</c> body is
/// streamed through and a chunked one sent on chunked; one past
/// <see cref="ExchangeLimits.MaxUploadBytes"/> is <c>413</c>. <c>Expect: 100-continue</c> is
/// forwarded and the origin's <c>100</c> relayed before the body. <c>Max-Forwards: 0</c> on
/// <c>OPTIONS</c> or <c>TRACE</c> is answered by the proxy; any other value is decremented. A
/// <c>101</c> turns the connection into a byte relay. The origin connection is kept while the
/// next request names the same <c>host:port</c>; one that closed between requests is
/// reconnected once for an idempotent request without a body. An origin response head the
/// proxy cannot read is <c>502</c>, one that does not arrive within the head timeout <c>504</c>.
/// </para>
/// <para>
/// <b><c>CONNECT</c>.</b> An authority-form target (<c>host:port</c>, IPv6 in brackets) to any
/// port 1 to 65535 is connected and answered exactly
/// <c>HTTP/1.1 200 Connection established</c> and an empty line; bytes are then relayed both ways
/// unread until both sides close. Any other target is <c>400</c>.
/// </para>
/// <para>
/// An origin that cannot be reached is answered by ADR-0088 decision 2's table - <c>403</c>
/// for one the allowed-origin rule refuses, <c>502</c> for one that did not resolve, refused or
/// was unreachable, <c>504</c> for one that did not accept in time - with its log line, and
/// curl's connection stays open unless the request asked to close it or carried a body. Every
/// answer the proxy makes itself has <c>Date</c>, <c>Server: surl</c> and a fixed-text body.
/// </para>
/// </remarks>
public sealed class HttpProxyProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter, IExchangeBytesRedactingServer
{
    private static readonly ReadOnlyMemory<byte> ServiceUnavailableResponse =
        "HTTP/1.1 503 Service Unavailable\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray();

    private readonly IOriginConnector originConnector;
    private readonly IAuthenticationPolicy proxyAuthenticationPolicy;

    /// <summary>
    /// Creates the proxy server with no proxy accounts: it asks for no credentials and relies on
    /// the allowed-origin rule (ADR-0088 decision 5), as <see cref="AnonymousAuthenticationPolicy"/>
    /// lets every request through.
    /// </summary>
    /// <param name="originConnector">How origins are reached: the composition root's, which applies the allowed-origin rule.</param>
    public HttpProxyProtocolServer(IOriginConnector originConnector)
        : this(originConnector, new AnonymousAuthenticationPolicy())
    {
    }

    /// <summary>
    /// Creates the proxy server whose every request and <c>CONNECT</c> is judged by
    /// <paramref name="proxyAuthenticationPolicy"/> (ADR-0088 decision 5): the policy built from
    /// the proxy's own accounts and methods, never the origin's.
    /// </summary>
    /// <param name="originConnector">How origins are reached: the composition root's, which applies the allowed-origin rule.</param>
    /// <param name="proxyAuthenticationPolicy">
    /// Who may use the proxy, and how. Its HTTP session judges <c>Proxy-Authorization</c> as if it
    /// were <c>Authorization</c>, and its challenges are sent as <c>Proxy-Authenticate</c>.
    /// </param>
    public HttpProxyProtocolServer(IOriginConnector originConnector, IAuthenticationPolicy proxyAuthenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(originConnector);
        ArgumentNullException.ThrowIfNull(proxyAuthenticationPolicy);

        this.originConnector = originConnector;
        this.proxyAuthenticationPolicy = proxyAuthenticationPolicy;
    }

    /// <summary>
    /// The proxy listen URL's schemes: <c>http</c>, and <c>https</c>, which is this same server
    /// on connections the serving engine has already secured with TLS (ADR-0088 decision 6).
    /// The proxy is chosen by <c>--proxy</c>, not by scheme (decision 7).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["http", "https"]);

    /// <summary>
    /// Creates the redactor of the exchange log for curl's connection, which withholds the
    /// credentials of the client's <c>Proxy-Authorization</c> and <c>Authorization</c> fields where
    /// it reads them (ADR-0182, amendment 8), and the values of the fields in
    /// <see cref="HttpWithheldHeaderNames.ApiKeysAndSessionTokens"/> (amendment 18). In HTTP/1.x
    /// request heads and chunked trailers, those inside an HTTP/1.1 <c>CONNECT</c> tunnel included,
    /// the credentials and those values are written as <c>****</c> (amendment 18 lists a head where
    /// they can still be logged; amendment 32 closed its trailers). Once the bytes are taken as a tunnel's text (a line where a request may start that is no request line, among the
    /// cases amendment 16 lists), then, line by line, the argument of <c>PASS</c> and <c>ACCT</c>,
    /// the password of an IMAP <c>LOGIN</c>, SASL <c>AUTH</c> and <c>AUTHENTICATE</c> responses,
    /// the lines answering <c>334</c> or <c>+</c>, those two fields' credentials and the API-key and
    /// session-token fields' values are written as
    /// <c>****</c> (amendment 16 also lists what is not masked). From an HTTP/2
    /// connection preface that starts where a request may start (on curl's connection to the proxy
    /// or inside such a tunnel), a header block carrying any of these fields is logged as its frame headers,
    /// each followed by <c>****</c> in place of its payload. A <c>DATA</c> frame on an HTTP/2
    /// <c>CONNECT</c> stream, or on any stream once the header blocks can no longer be followed, is
    /// logged the same way, an empty one as its frame header alone, so a credential tunnelled in it
    /// is not logged; <c>Surl.Http2</c>'s <c>Http2RequestBytesRedactor</c> says exactly what is withheld. After a
    /// preface anywhere else, every later byte is withheld. The log holds curl's side of the
    /// connection only; bytes the proxy sends to an origin are not logged.
    /// </summary>
    /// <param name="context">The exchange the redactor is for.</param>
    /// <returns>A redactor for curl's connection, used by that exchange alone.</returns>
    public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context) =>
        new HttpProxyConnectionBytesRedactor();

    /// <summary>
    /// Answers every request on <paramref name="connection"/> until curl closes it or an answer
    /// closes it, then closes any origin connection still kept.
    /// </summary>
    /// <param name="connection">curl's accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        await using var exchange = new HttpProxyExchange(connection, context, originConnector, proxyAuthenticationPolicy);
        try
        {
            await exchange.ServeAsync();
        }
        catch (IOException)
        {
            context.Log.Note("A connection failed mid-exchange; the exchange ended.");
        }
    }

    /// <summary>
    /// Refuses a connection over a connection limit with <c>503 Service Unavailable</c> and
    /// closes it.
    /// </summary>
    /// <param name="connection">The refused connection.</param>
    /// <param name="refusal">Which limit was reached.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the refusal is written.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await connection.WriteAsync(ServiceUnavailableResponse, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
