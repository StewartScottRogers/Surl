using System.Globalization;
using Surl.Protocol.Abstractions;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// Serves one HTTP/3 request stream (RFC 9114 section 4; ADR-0082 decisions 7 and 9): reads its
/// <c>HEADERS</c>, maps the request with <see cref="Http3RequestHeadMapping"/>, and hands it to the
/// <see cref="IHttp3RequestStreamHandler"/> as an <see cref="Http3RequestStream"/>.
/// </summary>
/// <remarks>
/// <para>
/// The head timeout runs from the stream's first byte: a <c>HEADERS</c> not whole within it, or a
/// stream that ends before one, is reset and stopped <c>H3_REQUEST_INCOMPLETE</c>. Unknown frame
/// types before it are skipped; <c>DATA</c> before it, and any frame not allowed on a request
/// stream, is <c>H3_FRAME_UNEXPECTED</c>. A <c>HEADERS</c> longer than <c>--max-message</c>, or that
/// decodes to a larger section, is answered <c>:status 431</c> with <c>FIN</c> and then
/// <c>STOP_SENDING</c> <c>H3_NO_ERROR</c>, without reading it; a section QPACK cannot decode is
/// <c>QPACK_DECOMPRESSION_FAILED</c>. A malformed request is reset and stopped <c>H3_MESSAGE_ERROR</c>.
/// </para>
/// <para>
/// A handler that returns with the request's body still arriving - a refusal, or a <c>413</c> past
/// the upload limit - is followed by <c>STOP_SENDING</c> <c>H3_NO_ERROR</c> (RFC 9114 section 4.1),
/// unless it already stopped the stream. Connection errors are thrown as
/// <see cref="Http3ConnectionException"/> for the connection to close with.
/// </para>
/// </remarks>
/// <param name="connection">The connection.</param>
/// <param name="reader">The request stream.</param>
/// <param name="handler">Serves each well-formed request.</param>
/// <param name="options">The server's options.</param>
/// <param name="context">The exchange: its clock, limits and log.</param>
/// <param name="fieldSections">The connection's QPACK.</param>
internal sealed class Http3RequestStreamServer(
    QuicApplicationConnection connection,
    Http3StreamReader reader,
    IHttp3RequestStreamHandler handler,
    Http3ServerOptions options,
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

        var (head, malformation) = Http3RequestHeadMapping.Map(decoded.FieldLines);
        if (head is null)
        {
            await AbortAsync(Http3ErrorCode.MessageError, $"the request is malformed: {malformation}", cancellationToken);
            return;
        }

        await HandAsync(head, cancellationToken);
    }

    // Null when the stream was refused or abandoned before a whole HEADERS frame arrived.
    private async Task<byte[]?> ReadHeadWithinTimeoutAsync(CancellationToken cancellationToken)
    {
        if (!await reader.HasMoreBeforeFinAsync(cancellationToken))
        {
            await AbortAsync(Http3ErrorCode.RequestIncomplete, "it ended before any frame", cancellationToken);
            return null;
        }

        using var headTimeout = new CancellationTokenSource(Http3HeadTimeoutDelay.For(context.Limits.HeadTimeout), context.TimeProvider);
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
        QpackFieldLine[] lines = options.ServerFieldValue is { } server
            ? [new(":status", "431"), new("server", server), new("content-length", "0")]
            : [new(":status", "431"), new("content-length", "0")];
        await connection.WriteAsync(Stream, Http3FrameWriter.Frame(Http3FrameType.Headers, fieldSections.Encode(lines)), true, cancellationToken);
        await connection.StopSendingAsync(Stream, (ulong)Http3ErrorCode.NoError, cancellationToken);
    }

    private async Task HandAsync(Http3RequestHead head, CancellationToken cancellationToken)
    {
        var isAnnounced = head.ContentLength > 0 || head.ExpectsContinue || (head.ContentLength is null && await reader.HasMoreAsync(cancellationToken));
        var stream = new Http3RequestStream(connection, reader, fieldSections, frameLimit, head, isAnnounced, context.Log);
        await handler.ServeRequestStreamAsync(stream, cancellationToken);
        if (stream.IsRequestBodyStillArriving && !stream.HasStoppedRequest)
        {
            await stream.StopRequestAsync(Http3ErrorCode.NoError, cancellationToken);
        }
    }

    // A stream error (RFC 9114 section 8): the answer side is reset and the client asked to stop.
    private async Task AbortAsync(Http3ErrorCode code, string reason, CancellationToken cancellationToken)
    {
        context.Log.Note($"Stream {Stream.Id} was reset with {code}: {reason}.");
        await connection.ResetAsync(Stream, (ulong)code, cancellationToken);
        await connection.StopSendingAsync(Stream, (ulong)code, cancellationToken);
    }
}
