using System.Globalization;
using Surl.Hpack;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The HTTP server's <see cref="IHttp2StreamHandler"/> (ADR-0081, decision 8; ADR-0086,
/// decision 1): builds an <see cref="HttpRequest"/> from each HTTP/2 request stream, has the
/// answerer decide its <see cref="HttpAnswer"/>, and writes that answer back as one header block
/// of <c>:status</c> and the answer's fields with lowercase names, then the body as <c>DATA</c>.
/// An answer with no body bytes ends the stream on its <c>HEADERS</c>.
/// </summary>
/// <remarks>
/// A request whose <c>DATA</c>, once read, did not add up to its <c>content-length</c> is reset
/// <c>PROTOCOL_ERROR</c> instead of answered. A content-store file that cannot be read whole after
/// the header block went out is ended with <c>RST_STREAM</c> <c>INTERNAL_ERROR</c> and a log note
/// naming the file, as HTTP/1.1 aborts the connection, since its <c>content-length</c> can no
/// longer be met; the connection carries on.
/// <para>
/// NTLM and Negotiate are logins bound to the connection, which HTTP/2 does not carry
/// (ADR-0081, decision 7; RFC 9113 section 8.2.3): a stream whose <c>authorization</c> names
/// either is reset <c>HTTP_1_1_REQUIRED</c> before the answerer sees it, so no handshake starts,
/// and so is one whose answer is a <c>401</c> challenging for nothing else. Curl then retries
/// over HTTP/1.1; the connection carries on.
/// </para>
/// </remarks>
/// <param name="answerer">Decides the answer to every request.</param>
/// <param name="context">The exchange: its limits and log.</param>
internal sealed class Http2StreamAnswerWriter(IHttpRequestHandler answerer, ExchangeContext context) : IHttp2StreamHandler
{
    /// <summary>
    /// Answers the request on <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The request stream.</param>
    /// <param name="cancellationToken">Cancelled when the client resets the stream or the connection ends.</param>
    /// <returns>A task that completes when the answer is written or the stream reset.</returns>
    /// <exception cref="OperationCanceledException">The stream or the connection ended.</exception>
    /// <exception cref="IOException">The client closed before granting the window the body needs, or the connection failed.</exception>
    public async Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken)
    {
        var head = stream.Head;
        if (ConnectionBoundLogins.AreCarriedBy(head.Fields))
        {
            await stream.ResetAsync(Http2ErrorCode.Http11Required, "its authorization names NTLM or Negotiate, which HTTP/2 does not carry");

            return;
        }

        var body = new Http2HttpRequestBody(stream, head.ExpectsContinue, context.Limits.MaxUploadBytes);
        var answer = await answerer.AnswerAsync(new HttpRequest(head.Method, head.Target, head.Authority, head.Fields, body), cancellationToken);
        if (stream.HasContentLengthMismatch)
        {
            await stream.ResetAsync(Http2ErrorCode.ProtocolError, "its DATA did not add up to its content-length");

            return;
        }

        if (ConnectionBoundLogins.AreTheOnlyLoginsOfferedBy(answer))
        {
            await stream.ResetAsync(Http2ErrorCode.Http11Required, "the only logins it could be challenged for are NTLM or Negotiate, which HTTP/2 does not carry");

            return;
        }

        await WriteAsync(stream, answer, cancellationToken);
    }

    private async Task WriteAsync(Http2RequestStream stream, HttpAnswer answer, CancellationToken cancellationToken)
    {
        var bodyLength = BodyLength(answer.Body);
        await stream.WriteHeadersAsync(HeaderFields(answer), bodyLength == 0);
        if (bodyLength == 0)
        {
            return;
        }

        await using var data = stream.CreateResponseBodyStream(bodyLength);
        if (answer.Body is HttpAnswerBody.Bytes bytes)
        {
            await data.WriteAsync(bytes.Content);

            return;
        }

        await CopyFileAsync(stream, (HttpAnswerBody.ContentFile)answer.Body, data, cancellationToken);
    }

    private static long BodyLength(HttpAnswerBody body) => body switch
    {
        HttpAnswerBody.Bytes bytes => bytes.Content.Length,
        HttpAnswerBody.ContentFile file => file.Range.ByteCount,
        _ => 0,
    };

    private static IEnumerable<HpackHeaderField> HeaderFields(HttpAnswer answer) =>
        answer.Fields
            .Select(field => new HpackHeaderField(field.Key.ToLowerInvariant(), field.Value))
            .Prepend(new HpackHeaderField(":status", answer.Status.Code.ToString(CultureInfo.InvariantCulture)));

    private async Task CopyFileAsync(Http2RequestStream stream, HttpAnswerBody.ContentFile file, Http2DataStream data, CancellationToken cancellationToken)
    {
        string? failure;
        try
        {
            var copied = await file.Store.CopyFileBytesAsync(file.Mapping, file.Range, data, cancellationToken);
            failure = copied < file.Range.ByteCount ? $"shrank to {copied} bytes while it was sent" : null;
        }
        catch (Exception readFailure) when (readFailure is IOException or UnauthorizedAccessException && !data.HasFailedWrite)
        {
            failure = $"could not be read after the 200 header block was sent ({readFailure.GetType().Name}: {readFailure.Message})";
        }

        if (failure is not null)
        {
            context.Log.Note($"{file.Mapping.Location} {failure}; stream {stream.Id} was reset with INTERNAL_ERROR.");
            await stream.ResetAsync(Http2ErrorCode.InternalError, "its content-store file could not be sent whole");
        }
    }
}
