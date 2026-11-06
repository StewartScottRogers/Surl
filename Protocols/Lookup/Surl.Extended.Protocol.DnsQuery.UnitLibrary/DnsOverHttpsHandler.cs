using System.Globalization;
using Surl.Hpack;
using Surl.Http2;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS server over HTTPS (RFC 8484; ADR-0092 decision 5): one handler that decides every
/// request, over HTTP/1.1 through <see cref="Http1ConnectionServer"/> and over HTTP/2 through
/// <see cref="Http2ConnectionServer"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The path is <see cref="Path"/>; any other is <c>404</c>.</item>
/// <item><c>GET</c> carries the query in its one <c>dns</c> parameter, base64url without padding;
/// none, two, or a padded or invalid value is <c>400</c>.</item>
/// <item><c>POST</c> carries it as a body of type <see cref="MediaType"/>; another type, or none,
/// is <c>415</c>; a body past <see cref="MaxQueryLength"/> bytes is <c>413</c> and one that cannot
/// be read whole is <c>400</c>, both closing an HTTP/1.1 connection.</item>
/// <item>Another method is <c>405</c> with <c>Allow: GET, POST</c>.</item>
/// <item>A query <see cref="DnsQueryAnswerer"/> drops - under 12 bytes, or a response - is
/// <c>400</c>, since HTTP must answer. Every other query is <c>200</c> with the DNS response,
/// whatever its RCODE, of type <see cref="MediaType"/>, with <c>Cache-Control: max-age</c> from
/// <see cref="DnsOverHttpsCacheLifetime"/>. The DNS ID is echoed as sent.</item>
/// <item>An HTTP/2 request whose <c>DATA</c> does not add up to its <c>content-length</c> is reset
/// with <c>PROTOCOL_ERROR</c> (RFC 9113 section 8.1.1).</item>
/// </list>
/// </remarks>
public sealed class DnsOverHttpsHandler : IHttpRequestHandler, IHttp2StreamHandler
{
    /// <summary>The one path answered (RFC 8484 section 4.1.1's).</summary>
    public const string Path = "/dns-query";

    /// <summary>The media type of a query and an answer (RFC 8484 section 6).</summary>
    public const string MediaType = "application/dns-message";

    /// <summary>The longest query and answer: the most a DNS message over a stream can hold.</summary>
    public const int MaxQueryLength = DnsTcpResponseWriter.MaxMessageLength;

    private static readonly HttpStatus UnsupportedMediaType = new(415, "Unsupported Media Type");

    private readonly DnsQueryAnswerer answerer;
    private readonly TimeProvider timeProvider;
    private readonly ExchangeContext? exchange;

    /// <summary>
    /// Creates a DNS over HTTPS handler that answers from <paramref name="zones"/>, dating its
    /// answers by <paramref name="timeProvider"/>; zone transfer allowed or not, AXFR and IXFR are
    /// <c>REFUSED</c> or <c>NOTIMP</c> (<see cref="DnsQueryAnswerer.AnswerOverHttp"/>). It asks no
    /// fault plan.
    /// </summary>
    public DnsOverHttpsHandler(DnsZoneSet zones, TimeProvider timeProvider, bool allowZoneTransfer = false)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        answerer = new DnsQueryAnswerer(zones, allowZoneTransfer);
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates a DNS over HTTPS handler for one exchange: it answers from <paramref name="zones"/>
    /// as the other constructor says, dates its answers by the exchange's clock, and offers each
    /// query to the exchange's fault plan first (<see cref="DnsQueryFaults"/>), whose chosen RCODE
    /// is answered <c>200</c> in place of the zones' answer.
    /// </summary>
    public DnsOverHttpsHandler(DnsZoneSet zones, ExchangeContext exchange, bool allowZoneTransfer = false)
        : this(zones, (exchange ?? throw new ArgumentNullException(nameof(exchange))).TimeProvider, allowZoneTransfer)
    {
        this.exchange = exchange;
    }

    /// <summary>Decides the answer to one DNS over HTTPS request, as the remarks say.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cuts reading a <c>POST</c>'s body off.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = request.Target.Split('?', 2);
        if (target[0] != Path)
        {
            return Empty(request, HttpStatus.NotFound);
        }

        return await (request.Method switch
        {
            "GET" => AnswerGetAsync(request, target.Length == 2 ? target[1] : string.Empty),
            "POST" => AnswerPostAsync(request, cancellationToken),
            _ => Task.FromResult(Empty(request, HttpStatus.MethodNotAllowed, [new("Allow", "GET, POST")])),
        });
    }

    /// <summary>
    /// Answers the request on one HTTP/2 stream as <see cref="AnswerAsync"/> decides it: one
    /// header block, then the DNS response as <c>DATA</c> ending the stream, or a reset when the
    /// request's <c>DATA</c> did not add up to its <c>content-length</c>.
    /// </summary>
    /// <param name="stream">The request stream.</param>
    /// <param name="cancellationToken">Cancelled when the client resets the stream or the connection ends.</param>
    /// <returns>A task that completes when the stream is answered.</returns>
    public async Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var head = stream.Head;
        var answer = await AnswerAsync(new HttpRequest(head.Method, head.Target, head.Authority, head.Fields, new DnsHttp2RequestBody(stream)), cancellationToken);
        if (stream.HasContentLengthMismatch)
        {
            await stream.ResetAsync(Http2ErrorCode.ProtocolError, "its DATA did not add up to its content-length");
            return;
        }

        var body = answer.Body is HttpAnswerBody.Bytes bytes ? bytes.Content : ReadOnlyMemory<byte>.Empty;
        await stream.WriteHeadersAsync(Http2Fields(answer), endStream: body.IsEmpty);
        if (!body.IsEmpty)
        {
            await stream.WriteDataAsync(body, endStream: true);
        }
    }

    private async Task<HttpAnswer> AnswerGetAsync(HttpRequest request, string query) =>
        DnsOverHttpsQueryParameter.Decode(query) is { } message ? await AnswerQueryAsync(request, message) : Empty(request, HttpStatus.BadRequest);

    private async Task<HttpAnswer> AnswerPostAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!IsDnsMessage(Field(request, "Content-Type")))
        {
            return Empty(request, UnsupportedMediaType);
        }

        if (request.Body.AnnouncedLength > MaxQueryLength)
        {
            return Refusal(HttpStatus.ContentTooLarge);
        }

        return request.Body.IsReadable
            ? await ReadAndAnswerAsync(request, cancellationToken)
            : Refusal(HttpStatus.BadRequest);
    }

    private async Task<HttpAnswer> ReadAndAnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new DnsOverHttpsQueryBuffer(MaxQueryLength);
        var outcome = request.Body.IsAnnounced
            ? await request.Body.CopyToAsync(buffer, cancellationToken)
            : HttpRequestBodyDiscardOutcome.Discarded;

        if (buffer.IsPastLimit || outcome == HttpRequestBodyDiscardOutcome.TooLarge)
        {
            return Refusal(HttpStatus.ContentTooLarge);
        }

        return outcome == HttpRequestBodyDiscardOutcome.Discarded
            ? await AnswerQueryAsync(request, buffer.ToArray())
            : Refusal(HttpStatus.BadRequest);
    }

    // The fault plan's chosen error, when this handler serves an exchange, else the answerer's.
    private async Task<DnsResponse?> AnswerOverHttpAsync(byte[] query) =>
        (exchange is null ? null : await DnsQueryFaults.FindErrorAsync(exchange, query)) ?? answerer.AnswerOverHttp(query);

    private async Task<HttpAnswer> AnswerQueryAsync(HttpRequest request, byte[] query)
    {
        if (await AnswerOverHttpAsync(query) is not { } response)
        {
            return Empty(request, HttpStatus.BadRequest);
        }

        var message = DnsResponseWriter.WriteWithin(response, MaxQueryLength);
        var maxAge = DnsOverHttpsCacheLifetime.MaxAgeOf(response.Message);

        return new HttpAnswer(
            HttpStatus.Ok,
            [
                .. CommonFields(),
                new("Content-Type", MediaType),
                new("Cache-Control", "max-age=" + maxAge.ToString(CultureInfo.InvariantCulture)),
                new("Content-Length", message.Length.ToString(CultureInfo.InvariantCulture)),
            ],
            new HttpAnswerBody.Bytes(message),
            IsRefusal: false,
            LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    private HttpAnswer Empty(HttpRequest request, HttpStatus status, IReadOnlyList<KeyValuePair<string, string>>? fields = null) =>
        new(status, [.. CommonFields(), .. fields ?? [], ContentLengthZero], HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);

    // A body that is too large or cannot be read whole: HTTP/1.1 closes the connection after it.
    private HttpAnswer Refusal(HttpStatus status) =>
        new(status, [.. CommonFields(), ContentLengthZero], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: true);

    private KeyValuePair<string, string>[] CommonFields() =>
    [
        new("Date", timeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture)),
        new("Server", HttpResponseHead.ServerName),
    ];

    private static readonly KeyValuePair<string, string> ContentLengthZero = new("Content-Length", "0");

    // The media type before any parameter, compared without case (RFC 9110 section 8.3.1).
    private static bool IsDnsMessage(string? contentType) =>
        contentType is not null && string.Equals(contentType.Split(';')[0].Trim(), MediaType, StringComparison.OrdinalIgnoreCase);

    private static string? Field(HttpRequest request, string name) =>
        request.Fields.FirstOrDefault(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static IEnumerable<HpackHeaderField> Http2Fields(HttpAnswer answer) =>
        answer.Fields
            .Select(field => new HpackHeaderField(field.Key.ToLowerInvariant(), field.Value))
            .Prepend(new HpackHeaderField(":status", answer.Status.Code.ToString(CultureInfo.InvariantCulture)));
}
