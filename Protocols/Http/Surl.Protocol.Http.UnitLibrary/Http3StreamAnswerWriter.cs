using System.Globalization;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using Surl.Qpack;

namespace Surl.Protocol.Http;

/// <summary>
/// The HTTP server's <see cref="IHttp3RequestStreamHandler"/> (ADR-0082 decision 9; ADR-0086,
/// decision 2): builds an <see cref="HttpRequest"/> from each HTTP/3 request stream, has the
/// answerer decide its <see cref="HttpAnswer"/>, and writes that answer back as a <c>HEADERS</c>
/// frame of <c>:status</c> and the answer's fields with lowercase names, then the body as
/// <c>DATA</c> frames, then <c>FIN</c>. An answer with no body bytes ends the stream on its
/// <c>HEADERS</c>.
/// </summary>
/// <remarks>
/// A request whose <c>DATA</c>, once read, did not add up to its <c>content-length</c> is reset
/// and stopped <c>H3_MESSAGE_ERROR</c> instead of answered. A content-store file that cannot be
/// read whole once its <c>HEADERS</c> went out is reset <c>H3_INTERNAL_ERROR</c>, as HTTP/2 resets
/// it <c>INTERNAL_ERROR</c>, with a log note naming the file; the connection carries on.
/// <para>
/// NTLM and Negotiate are logins bound to the connection, which HTTP/3 does not carry (ADR-0082,
/// decision 9): a stream whose <c>authorization</c> names either is reset and stopped
/// <c>H3_VERSION_FALLBACK</c> before the answerer sees it, so no handshake starts, and so is one
/// whose answer is a <c>401</c> challenging for nothing else, as <see cref="Http2StreamAnswerWriter"/>
/// resets them <c>HTTP_1_1_REQUIRED</c>. The connection carries on.
/// </para>
/// </remarks>
/// <param name="answerer">Decides the answer to every request.</param>
/// <param name="context">The exchange: its limits and log.</param>
internal sealed class Http3StreamAnswerWriter(IHttpRequestHandler answerer, ExchangeContext context) : IHttp3RequestStreamHandler
{
    /// <summary>
    /// Answers the request on <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The request stream.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the answer is written or the stream reset.</returns>
    public async Task ServeRequestStreamAsync(Http3RequestStream stream, CancellationToken cancellationToken)
    {
        var head = stream.Head;
        if (ConnectionBoundLogins.AreCarriedBy(head.Fields))
        {
            await stream.ResetAsync(Http3ErrorCode.VersionFallback, "its authorization names NTLM or Negotiate, which HTTP/3 does not carry", cancellationToken);
            return;
        }

        var body = new Http3HttpRequestBody(stream, context.Limits.MaxUploadBytes);
        var answer = await answerer.AnswerAsync(new HttpRequest(head.Method, head.Target, head.Authority, head.Fields, body), cancellationToken);
        if (stream.HasContentLengthMismatch)
        {
            await stream.ResetAsync(Http3ErrorCode.MessageError, "its DATA did not add up to its content-length", cancellationToken);
            return;
        }

        if (ConnectionBoundLogins.AreTheOnlyLoginsOfferedBy(answer))
        {
            await stream.ResetAsync(Http3ErrorCode.VersionFallback, "the only logins it could be challenged for are NTLM or Negotiate, which HTTP/3 does not carry", cancellationToken);
            return;
        }

        if (answer.DropsExchange)
        {
            await stream.ResetAsync(Http3ErrorCode.InternalError, "a fault rule dropped the exchange", cancellationToken);
            return;
        }

        await WriteAnswerAsync(stream, answer, cancellationToken);
    }

    private async Task WriteAnswerAsync(Http3RequestStream stream, HttpAnswer answer, CancellationToken cancellationToken)
    {
        switch (answer.Body)
        {
            case HttpAnswerBody.Bytes { Content.IsEmpty: false } bytes:
                await stream.WriteHeadersAsync(FieldLines(answer), endStream: false, cancellationToken);
                await stream.WriteDataAsync(bytes.Content, endStream: true, cancellationToken);
                break;
            case HttpAnswerBody.ContentFile { Range.ByteCount: > 0 } file:
                await stream.WriteHeadersAsync(FieldLines(answer), endStream: false, cancellationToken);
                await CopyFileAsync(stream, file, cancellationToken);
                break;
            default:
                await stream.WriteHeadersAsync(FieldLines(answer), endStream: true, cancellationToken);
                break;
        }
    }

    private static IEnumerable<QpackFieldLine> FieldLines(HttpAnswer answer) =>
        answer.Fields
            .Select(field => new QpackFieldLine(field.Key.ToLowerInvariant(), field.Value))
            .Prepend(new QpackFieldLine(":status", answer.Status.Code.ToString(CultureInfo.InvariantCulture)));

    private async Task CopyFileAsync(Http3RequestStream stream, HttpAnswerBody.ContentFile file, CancellationToken cancellationToken)
    {
        // Writes to a QUIC stream throw no IOException, so one here is the file's.
        await using var data = stream.CreateResponseBodyStream();
        string? failure;
        try
        {
            var copied = await file.Store.CopyFileBytesAsync(file.Mapping, file.Range, data, cancellationToken);
            failure = copied < file.Range.ByteCount ? $"shrank to {copied} bytes while it was sent" : null;
        }
        catch (Exception readFailure) when (readFailure is IOException or UnauthorizedAccessException)
        {
            failure = $"could not be read after its HEADERS was sent ({readFailure.GetType().Name}: {readFailure.Message})";
        }

        if (failure is null)
        {
            await stream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true, cancellationToken);
            return;
        }

        context.Log.Note($"{file.Mapping.Location} {failure}; stream {stream.Id} was reset with H3_INTERNAL_ERROR.");
        await stream.ResetResponseAsync(Http3ErrorCode.InternalError, cancellationToken);
    }
}
