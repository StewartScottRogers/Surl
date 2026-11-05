using System.Globalization;
using Surl.Hpack;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// Turns an <see cref="HttpAnswer"/> into frames on one HTTP/2 stream (ADR-0081, decision 8):
/// one header block of <c>:status</c> and the answer's fields with lowercase names, then the body
/// as <c>DATA</c> within the client's windows. An answer with no body bytes ends the stream on its
/// <c>HEADERS</c>.
/// </summary>
/// <remarks>
/// A content-store file that cannot be read whole after the header block went out is ended with
/// <c>RST_STREAM</c> <c>INTERNAL_ERROR</c> and a log note naming the file, as HTTP/1.1 aborts the
/// connection, since its <c>content-length</c> can no longer be met; the connection carries on.
/// </remarks>
/// <param name="frames">Writes the frames.</param>
/// <param name="windows">The connection's send windows.</param>
/// <param name="context">The exchange: its log and cancellation.</param>
internal sealed class Http2StreamAnswerWriter(Http2FrameWriter frames, Http2SendWindows windows, ExchangeContext context)
{
    /// <summary>
    /// Writes <paramref name="answer"/> on <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The stream answered.</param>
    /// <param name="answer">The answer.</param>
    /// <returns>A task that completes when the answer is written or the stream reset.</returns>
    /// <exception cref="OperationCanceledException">The stream or the connection ended.</exception>
    /// <exception cref="IOException">The client closed before granting the window the body needs, or the connection failed.</exception>
    public async Task WriteAsync(Http2Stream stream, HttpAnswer answer)
    {
        var bodyLength = BodyLength(answer.Body);
        await frames.WriteHeaderBlockAsync(stream.Id, HeaderFields(answer), bodyLength == 0, context.CancellationToken);
        if (bodyLength == 0)
        {
            return;
        }

        await using var data = new Http2DataStream(frames, windows, stream.Id, bodyLength, stream.Token, context.CancellationToken);
        if (answer.Body is HttpAnswerBody.Bytes bytes)
        {
            await data.WriteAsync(bytes.Content);

            return;
        }

        await CopyFileAsync(stream, (HttpAnswerBody.ContentFile)answer.Body, data);
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

    private async Task CopyFileAsync(Http2Stream stream, HttpAnswerBody.ContentFile file, Http2DataStream data)
    {
        string? failure;
        try
        {
            var copied = await file.Store.CopyFileBytesAsync(file.Mapping, file.Range, data, stream.Token);
            failure = copied < file.Range.ByteCount ? $"shrank to {copied} bytes while it was sent" : null;
        }
        catch (Exception readFailure) when (readFailure is IOException or UnauthorizedAccessException && !data.HasFailedWrite)
        {
            failure = $"could not be read after the 200 header block was sent ({readFailure.GetType().Name}: {readFailure.Message})";
        }

        if (failure is not null)
        {
            context.Log.Note($"{file.Mapping.Location} {failure}; stream {stream.Id} was reset with INTERNAL_ERROR.");
            await frames.WriteAsync(new Http2RstStreamFrame(stream.Id, Http2ErrorCode.InternalError), context.CancellationToken);
        }
    }
}
