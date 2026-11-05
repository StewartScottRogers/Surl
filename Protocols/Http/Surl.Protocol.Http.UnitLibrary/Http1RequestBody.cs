using System.Security.Cryptography;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The body of an HTTP/1.x request, as its head frames it (RFC 9112, section 6): read
/// through the connection's reader after <c>100 Continue</c> when the request expects one
/// (ADR-0027).
/// </summary>
internal sealed class Http1RequestBody : IHttpRequestBody
{
    // A body that was read to its end leaves none to read.
    private static readonly HttpRequestBodyFraming BodyAlreadyRead = new(HttpRequestBodyFramingKind.None, 0);

    private readonly IConnection connection;
    private readonly HttpRequestHead head;
    private readonly HttpRequestBodyDiscarder bodyDiscarder;
    private HttpRequestBodyFraming framing;

    /// <summary>
    /// Creates the body of one request.
    /// </summary>
    /// <param name="connection">Where <c>100 Continue</c> is written.</param>
    /// <param name="head">The request head.</param>
    /// <param name="framing">
    /// The body's framing; never <see cref="HttpRequestBodyFramingKind.InvalidContentLength"/>,
    /// which is refused before a body is made.
    /// </param>
    /// <param name="bodyDiscarder">Reads the body through the connection's reader.</param>
    public Http1RequestBody(IConnection connection, HttpRequestHead head, HttpRequestBodyFraming framing, HttpRequestBodyDiscarder bodyDiscarder)
    {
        this.connection = connection;
        this.head = head;
        this.framing = framing;
        this.bodyDiscarder = bodyDiscarder;
    }

    /// <inheritdoc/>
    public bool IsAnnounced => framing.Kind != HttpRequestBodyFramingKind.None;

    /// <inheritdoc/>
    public long? AnnouncedLength => framing.Kind == HttpRequestBodyFramingKind.ContentLength ? framing.ContentLength : null;

    /// <inheritdoc/>
    public bool IsReadable => framing.Kind != HttpRequestBodyFramingKind.Unreadable;

    /// <summary>
    /// Reads the body to its end and throws its bytes away, after <c>100 Continue</c> when the
    /// request announced a body and expects it.
    /// </summary>
    /// <param name="bodyHash">Where the body's bytes are hashed, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>How the read ended.</returns>
    /// <remarks>
    /// RFC 9110, section 10.1.1: a server that reads the content of a request expecting
    /// 100-continue sends 100 Continue first, so the client need not wait out its own timeout
    /// (curl's --expect100-timeout, one second) before it sends the body (ADR-0027).
    /// </remarks>
    public async Task<HttpRequestBodyDiscardOutcome> DiscardAsync(IncrementalHash? bodyHash, CancellationToken cancellationToken)
    {
        if (IsAnnounced && ExpectsContinue(head))
        {
            await connection.WriteAsync(new HttpResponseHead(HttpStatus.Continue).ToBytes(), cancellationToken);
        }

        var outcome = await bodyDiscarder.DiscardAsync(framing, bodyHash, cancellationToken);
        if (outcome == HttpRequestBodyDiscardOutcome.Discarded)
        {
            framing = BodyAlreadyRead;
        }

        return outcome;
    }

    // The expectation is ignored in an HTTP/1.0 request (RFC 9110, section 10.1.1). Expect is
    // a list, and 100-continue is matched without regard to case.
    private static bool ExpectsContinue(HttpRequestHead head) =>
        head.Version.Minor >= 1
        && head.GetFieldValues("Expect")
            .SelectMany(value => value.Split(','))
            .Any(expectation => string.Equals(expectation.Trim(' ', '\t'), "100-continue", StringComparison.OrdinalIgnoreCase));
}
