using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Protocol.Http;

/// <summary>
/// Serves one HTTP/3 request stream (RFC 9114 section 4; ADR-0082 decisions 7 and 9): reads its
/// <c>HEADERS</c>, maps the request as HTTP/2 does, answers it through the
/// <see cref="IHttpRequestHandler"/>, and writes the <see cref="HttpAnswer"/> as <c>HEADERS</c> and
/// <c>DATA</c> frames, then <c>FIN</c>.
/// </summary>
/// <remarks>
/// <para>
/// The head timeout runs from the stream's first byte: a <c>HEADERS</c> not whole within it, or a
/// stream that ends before one, is reset and stopped <c>H3_REQUEST_INCOMPLETE</c>. Unknown frame
/// types before it are skipped; <c>DATA</c> before it, and any frame not allowed on a request
/// stream, is <c>H3_FRAME_UNEXPECTED</c>. A <c>HEADERS</c> longer than <c>--max-message</c>, or that
/// decodes to a larger section, is answered <c>:status 431</c> with <c>FIN</c> and then
/// <c>STOP_SENDING</c> <c>H3_NO_ERROR</c>, without reading it; a section QPACK cannot decode is
/// <c>QPACK_DECOMPRESSION_FAILED</c>. A malformed request (<see cref="Http2RequestHeadMapping"/>) is
/// reset and stopped <c>H3_MESSAGE_ERROR</c>, as is one whose <c>DATA</c> does not add up to its
/// <c>content-length</c>.
/// </para>
/// <para>
/// An answer that leaves the request's body unread - a refusal, or a <c>413</c> past the upload
/// limit - is followed by <c>STOP_SENDING</c> <c>H3_NO_ERROR</c> (RFC 9114 section 4.1). A
/// content-store file that cannot be read whole once its <c>HEADERS</c> went out is reset
/// <c>H3_INTERNAL_ERROR</c>, as HTTP/2 resets it <c>INTERNAL_ERROR</c>. Connection errors are thrown
/// as <see cref="Http3ConnectionException"/> for the connection to close with.
/// </para>
/// </remarks>
/// <param name="connection">The connection.</param>
/// <param name="reader">The request stream.</param>
/// <param name="handler">Decides each answer.</param>
/// <param name="context">The exchange: its clock, limits and log.</param>
/// <param name="fieldSections">The connection's QPACK.</param>
internal sealed class Http3RequestStreamServer(
    QuicApplicationConnection connection,
    Http3StreamReader reader,
    IHttpRequestHandler handler,
    ExchangeContext context,
    Http3FieldSections fieldSections)
{
    private readonly Http3FrameLimit frameLimit = new(context.Limits.MaxMessageBytes);

    private QuicStream Stream => reader.Stream;

    /// <summary>Serves the stream until it is answered, refused or abandoned.</summary>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the stream is done.</returns>
    /// <exception cref="Http3ConnectionException">The client broke a rule that ends the connection.</exception>
    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ServeRequestAsync(cancellationToken);
        }
        catch (Http3StreamAbandonedException abandoned)
        {
            context.Log.Note(abandoned.Message);
        }
        catch (InvalidOperationException stopped) when (Stream.StopSendingErrorCode is not null)
        {
            context.Log.Note($"The client stopped stream {Stream.Id} with STOP_SENDING 0x{Stream.StopSendingErrorCode:x} before its answer was sent: {stopped.Message}");
        }
    }

    private async Task ServeRequestAsync(CancellationToken cancellationToken)
    {
        if (await ReadHeadWithinTimeoutAsync(cancellationToken) is not { } section)
        {
            return;
        }

        var decoded = fieldSections.Decode(Stream.Id, section);
        if (decoded.Error == QpackDecodingError.FieldSectionTooLarge)
        {
            await AnswerFieldSectionTooLargeAsync($"its field section decodes past the {context.Limits.MaxMessageBytes}-byte message limit", cancellationToken);
            return;
        }

        if (!decoded.IsDecoded)
        {
            throw new Http3ConnectionException(Http3ErrorCode.QpackDecompressionFailed, $"the field section of stream {Stream.Id} could not be decoded: {decoded.Error}");
        }

        var (head, malformation) = Http2RequestHeadMapping.Map([.. decoded.FieldLines.Select(line => new Surl.Hpack.HpackHeaderField(line.Name, line.Value))]);
        if (head is null)
        {
            await AbortAsync(Http3ErrorCode.MessageError, $"the request is malformed: {malformation}", cancellationToken);
            return;
        }

        await AnswerAsync(head, cancellationToken);
    }

    // Null when the stream was refused or abandoned before a whole HEADERS frame arrived.
    private async Task<byte[]?> ReadHeadWithinTimeoutAsync(CancellationToken cancellationToken)
    {
        if (!await reader.HasMoreBeforeFinAsync(cancellationToken))
        {
            await AbortAsync(Http3ErrorCode.RequestIncomplete, "it ended before any frame", cancellationToken);
            return null;
        }

        using var headTimeout = new CancellationTokenSource(HeadTimeoutDelay.For(context.Limits.HeadTimeout), context.TimeProvider);
        using var headTimeoutOrConnection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, headTimeout.Token);
        try
        {
            return await ReadHeadersFrameAsync(headTimeoutOrConnection.Token, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await AbortAsync(Http3ErrorCode.RequestIncomplete, "its HEADERS was not whole within the head timeout", cancellationToken);
            return null;
        }
    }

    private async Task<byte[]?> ReadHeadersFrameAsync(CancellationToken readToken, CancellationToken cancellationToken)
    {
        while (await reader.ReadFrameHeaderAsync(readToken) is { } header)
        {
            if (header.Type == Http3FrameType.Headers)
            {
                return frameLimit.IsTooLong(header)
                    ? await AnswerHeadersTooLongAsync(header, cancellationToken)
                    : await reader.ReadExactlyAsync((int)header.Length, readToken);
            }

            if (header.Type == Http3FrameType.Data || Http3FrameType.IsUnexpectedOnRequestStream(header.Type))
            {
                throw new Http3ConnectionException(Http3ErrorCode.FrameUnexpected, $"a frame of type 0x{header.Type:x} arrived on request stream {Stream.Id} before its HEADERS");
            }

            frameLimit.Check(header, Stream.Id);
            await reader.SkipAsync(header.Length, readToken);
        }

        await AbortAsync(Http3ErrorCode.RequestIncomplete, "it ended before its HEADERS", cancellationToken);
        return null;
    }

    private async Task<byte[]?> AnswerHeadersTooLongAsync(Http3FrameHeader header, CancellationToken cancellationToken)
    {
        await AnswerFieldSectionTooLargeAsync($"its {header.Length}-byte HEADERS is past the {frameLimit.MaxLength}-byte message limit", cancellationToken);
        return null;
    }

    // ADR-0082 decision 7: 431 with FIN, then STOP_SENDING H3_NO_ERROR; the connection carries on.
    private async Task AnswerFieldSectionTooLargeAsync(string reason, CancellationToken cancellationToken)
    {
        context.Log.Note($"Stream {Stream.Id} was answered 431 and stopped with H3_NO_ERROR: {reason}.");
        var answer = new HttpAnswer(
            HttpStatus.RequestHeaderFieldsTooLarge,
            [new("Server", HttpResponseHead.ServerName), new("Content-Length", "0")],
            HttpAnswerBody.None,
            IsRefusal: true,
            LeavesRequestBodyUnread: true);
        await WriteHeadersAsync(answer, fin: true, cancellationToken);
        await connection.StopSendingAsync(Stream, (ulong)Http3ErrorCode.NoError, cancellationToken);
    }

    private async Task AnswerAsync(Http2RequestHead head, CancellationToken cancellationToken)
    {
        var isAnnounced = head.ContentLength > 0 || head.ExpectsContinue || (head.ContentLength is null && await reader.HasMoreAsync(cancellationToken));
        var body = new Http3RequestBody(
            reader,
            fieldSections,
            frameLimit,
            head,
            isAnnounced,
            context.Limits.MaxUploadBytes,
            () => WriteHeadersAsync(new HttpAnswer(HttpStatus.Continue, [], HttpAnswerBody.None, false, false), fin: false, cancellationToken));
        var answer = await handler.AnswerAsync(new HttpRequest(head.Method, head.Target, head.Authority, head.Fields, body), cancellationToken);
        if (body.HasContentLengthMismatch)
        {
            await AbortAsync(Http3ErrorCode.MessageError, "its DATA did not add up to its content-length", cancellationToken);
            return;
        }

        await WriteAnswerAsync(answer, cancellationToken);
        if (body.IsStillArriving)
        {
            await connection.StopSendingAsync(Stream, (ulong)Http3ErrorCode.NoError, cancellationToken);
        }
    }

    private async Task WriteAnswerAsync(HttpAnswer answer, CancellationToken cancellationToken)
    {
        switch (answer.Body)
        {
            case HttpAnswerBody.Bytes { Content.IsEmpty: false } bytes:
                await WriteHeadersAsync(answer, fin: false, cancellationToken);
                await connection.WriteAsync(Stream, Http3FrameWriter.Frame(Http3FrameType.Data, bytes.Content.Span), true, cancellationToken);
                break;
            case HttpAnswerBody.ContentFile { Range.ByteCount: > 0 } file:
                await WriteHeadersAsync(answer, fin: false, cancellationToken);
                await CopyFileAsync(file, cancellationToken);
                break;
            default:
                await WriteHeadersAsync(answer, fin: true, cancellationToken);
                break;
        }
    }

    private async Task WriteHeadersAsync(HttpAnswer answer, bool fin, CancellationToken cancellationToken)
    {
        var lines = answer.Fields
            .Select(field => new QpackFieldLine(field.Key.ToLowerInvariant(), field.Value))
            .Prepend(new QpackFieldLine(":status", answer.Status.Code.ToString(CultureInfo.InvariantCulture)));
        await connection.WriteAsync(Stream, Http3FrameWriter.Frame(Http3FrameType.Headers, fieldSections.Encode(lines)), fin, cancellationToken);
    }

    private async Task CopyFileAsync(HttpAnswerBody.ContentFile file, CancellationToken cancellationToken)
    {
        // Writes to a QUIC stream throw no IOException, so one here is the file's.
        var data = new Http3DataStream((bytes, token) => connection.WriteAsync(Stream, Http3FrameWriter.Frame(Http3FrameType.Data, bytes.Span), false, token));
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
            await connection.WriteAsync(Stream, ReadOnlyMemory<byte>.Empty, true, cancellationToken);
            return;
        }

        context.Log.Note($"{file.Mapping.Location} {failure}; stream {Stream.Id} was reset with H3_INTERNAL_ERROR.");
        await connection.ResetAsync(Stream, (ulong)Http3ErrorCode.InternalError, cancellationToken);
    }

    // A stream error (RFC 9114 section 8): the answer side is reset and the client asked to stop.
    private async Task AbortAsync(Http3ErrorCode code, string reason, CancellationToken cancellationToken)
    {
        context.Log.Note($"Stream {Stream.Id} was reset with {code}: {reason}.");
        await connection.ResetAsync(Stream, (ulong)code, cancellationToken);
        await connection.StopSendingAsync(Stream, (ulong)code, cancellationToken);
    }
}
