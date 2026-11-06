using System.Globalization;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// One curl connection to the proxy: reads each request head, and forwards it, tunnels it or
/// answers it itself, as <see cref="HttpProxyProtocolServer"/> describes. Holds the origin
/// connection kept between requests.
/// </summary>
internal sealed partial class HttpProxyExchange : IAsyncDisposable
{
    // Every other failure to read a head is a malformed head: 400, as the HTTP server answers it.
    private static readonly Dictionary<HttpRequestHeadReadOutcome, HttpStatus> StatusesForHeadsNotRead = new()
    {
        [HttpRequestHeadReadOutcome.UnsupportedVersion] = HttpStatus.HttpVersionNotSupported,
        [HttpRequestHeadReadOutcome.HeadTooLarge] = HttpStatus.RequestHeaderFieldsTooLarge,
        [HttpRequestHeadReadOutcome.HeadTimedOut] = HttpStatus.RequestTimeout,
    };

    private readonly IConnection client;
    private readonly HttpConnectionReader reader;
    private readonly ExchangeContext context;
    private readonly IOriginConnector originConnector;
    private OriginLink? origin;

    /// <summary>
    /// Creates the exchange for one accepted connection.
    /// </summary>
    /// <param name="client">curl's connection.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="originConnector">How origins are reached.</param>
    public HttpProxyExchange(IConnection client, ExchangeContext context, IOriginConnector originConnector)
    {
        this.client = client;
        this.context = context;
        this.originConnector = originConnector;
        reader = new HttpConnectionReader(client, context.Limits.MaxRequestHeadBytes);
    }

    private CancellationToken Cancellation => context.CancellationToken;

    /// <summary>
    /// Answers every request on curl's connection until curl closes it or an answer closes it.
    /// </summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        var isFirstHead = true;
        var keepsConnectionOpen = NotesTlsToTheProxy();
        while (keepsConnectionOpen)
        {
            var result = await reader.ReadNextRequestHeadAsync(context, isFirstHead);
            isFirstHead = false;
            keepsConnectionOpen = result.Head is { } head
                ? await AnswerRequestAsync(head)
                : await AnswerHeadNotReadAsync(result.Outcome);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await DropOriginAsync();

    /// <summary>
    /// On a connection secured before the first request (the HTTPS proxy, ADR-0088 decision 6
    /// and Amendment 2), notes that curl's connection to the proxy is TLS; true unless the
    /// handshake agreed an ALPN protocol other than <c>http/1.1</c>, which the proxy does not
    /// serve, so the connection closes without a byte written. A plain connection notes nothing.
    /// </summary>
    private bool NotesTlsToTheProxy()
    {
        if (client.TlsSession is not { } tls)
        {
            return true;
        }

        var applicationProtocol = tls.ApplicationProtocol ?? "none";
        if (tls.ApplicationProtocol is not (null or "http/1.1"))
        {
            context.Log.Note($"HTTPS proxy: the TLS handshake agreed ALPN {applicationProtocol}, which the proxy does not serve; the connection closes.");

            return false;
        }

        context.Log.Note($"HTTPS proxy: curl's connection to the proxy is TLS ({tls.Protocol}, ALPN {applicationProtocol}); requests are read inside it.");

        return true;
    }

    // Whether curl's connection ends after this request whatever the answer: HTTP/1.0, or a
    // Connection or Proxy-Connection field saying close (RFC 9112 section 9.3).
    private static bool RequestEndsConnection(HttpRequestHead head) =>
        head.Version.Minor == 0
        || ProxyFieldRules.ListsToken(head.Fields, "Connection", "close")
        || ProxyFieldRules.ListsToken(head.Fields, "Proxy-Connection", "close");

    private static byte[] FixedText(HttpStatus status) =>
        Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{status.Code} {status.ReasonPhrase}\n"));

    private Task<bool> AnswerHeadNotReadAsync(HttpRequestHeadReadOutcome outcome)
    {
        if (outcome is HttpRequestHeadReadOutcome.ConnectionClosed
            or HttpRequestHeadReadOutcome.ConnectionClosedBeforeHeadEnded
            or HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte)
        {
            context.Log.Note($"No request head was read: {outcome}; the connection closes.");

            return Task.FromResult(false);
        }

        var status = StatusesForHeadsNotRead.GetValueOrDefault(outcome, HttpStatus.BadRequest);

        return AnswerAsync(status, $"no request head was read ({outcome})", closes: true);
    }

    private Task<bool> AnswerRequestAsync(HttpRequestHead head)
    {
        if (head.GetFieldValues("Host").Count > 1)
        {
            return AnswerAsync(HttpStatus.BadRequest, "the request carries more than one Host field", closes: true);
        }

        return string.Equals(head.Method, "CONNECT", StringComparison.Ordinal)
            ? AnswerConnectAsync(head)
            : AnswerForwardableAsync(head);
    }

    /// <summary>
    /// Writes an answer the proxy makes itself: the status, <c>Date</c>, <c>Server: surl</c>,
    /// a fixed-text body naming the status (ADR-0006 section 3), and <c>Connection: close</c>
    /// when the connection closes after it; notes it in the log.
    /// </summary>
    private async Task<bool> AnswerAsync(HttpStatus status, string why, bool closes)
    {
        var body = FixedText(status);
        var head = NewHead(status)
            .AddField("Content-Type", "text/plain; charset=us-ascii")
            .AddField("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))
            .AddField("Connection", closes ? "close" : null);

        return await WriteAnswerAsync(head, body, $"Answered {status.Code}: {why}.", closes);
    }

    private HttpResponseHead NewHead(HttpStatus status) =>
        new HttpResponseHead(status)
            .AddField("Date", context.TimeProvider.GetUtcNow().ToString("R", CultureInfo.InvariantCulture))
            .AddField("Server", HttpResponseHead.ServerName);

    private async Task<bool> WriteAnswerAsync(HttpResponseHead head, byte[] body, string note, bool closes)
    {
        context.Log.Note(note);
        await client.WriteAsync(head.ToBytes(), Cancellation);
        await client.WriteAsync(body, Cancellation);
        if (closes)
        {
            await client.CompleteWritesAsync(Cancellation);
        }

        return !closes;
    }

    private async Task<OriginLink> ConnectAsync(ProxyTarget target)
    {
        var connection = await originConnector.ConnectAsync(target.Host, target.Port, context.Limits.HeadTimeout, Cancellation);

        return new OriginLink(target.Host, target.Port, connection);
    }

    private async Task DropOriginAsync()
    {
        if (origin is { } link)
        {
            origin = null;
            await link.DisposeAsync();
        }
    }
}
