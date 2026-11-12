using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.HttpServing;

/// <summary>
/// Writes an <see cref="HttpAnswer"/> to an HTTP/1.x connection: the status line naming
/// HTTP/1.1, the answer's fields and the <c>Connection</c> field after them, then the body,
/// then a half-close and the lingering-close drain when the connection closes (ADR-0024;
/// ADR-0081, decision 8). It says whether the connection stays open for another request.
/// </summary>
internal sealed class Http1AnswerWriter
{
    /// <summary>
    /// How long a refusal - any response that closes the connection because of the request -
    /// may take to write before the server gives up on it (ADR-0006, section 5).
    /// </summary>
    public static readonly TimeSpan RefusalWriteDeadline = TimeSpan.FromSeconds(1);

    private readonly IConnection connection;
    private readonly HttpUnreadRequestDrainer unreadRequestDrainer;
    private readonly ExchangeContext context;

    /// <summary>
    /// Creates a writer for one connection.
    /// </summary>
    /// <param name="connection">Where answers are written.</param>
    /// <param name="unreadRequestDrainer">Drains what the client still sends once the connection is half-closed.</param>
    /// <param name="context">The exchange: its clock, log and cancellation.</param>
    public Http1AnswerWriter(IConnection connection, HttpUnreadRequestDrainer unreadRequestDrainer, ExchangeContext context)
    {
        this.connection = connection;
        this.unreadRequestDrainer = unreadRequestDrainer;
        this.context = context;
    }

    /// <summary>
    /// Writes the answer to the request <paramref name="head"/> began. The connection stays
    /// open when the answer is no refusal, left no request body unread, and the request's
    /// version and <c>Connection</c> field keep it open (RFC 9112, section 9.3).
    /// </summary>
    /// <param name="answer">The answer.</param>
    /// <param name="head">The head of the request answered.</param>
    /// <returns><see langword="true"/> when the connection stays open for another request.</returns>
    public async Task<bool> WriteAsync(HttpAnswer answer, HttpRequestHead head)
    {
        if (answer.DropsExchange)
        {
            connection.Abort();

            return false;
        }

        if (answer.IsRefusal)
        {
            return await WriteRefusalAsync(answer);
        }

        if (answer.Takeover is { } takeover)
        {
            await WriteTakeoverAsync(answer, takeover);

            return false;
        }

        // A streamed body is chunked over HTTP/1.1; HTTP/1.0 has no chunked coding, so there the close ends it.
        var isStreamed = answer.Body is HttpAnswerBody.Streamed;
        var isChunked = isStreamed && head.Version.Minor >= 1;
        var keepsConnectionOpen = HttpConnectionPersistence.KeepsConnectionOpen(head) && !answer.LeavesRequestBodyUnread && isStreamed == isChunked;
        var responseHead = ResponseHead(answer, null)
            .AddField("Transfer-Encoding", isChunked ? "chunked" : null)
            .AddField("Connection", HttpConnectionPersistence.ConnectionFieldValue(keepsConnectionOpen, head.Version));
        await connection.WriteAsync(responseHead.ToBytes(), context.CancellationToken);

        if (!await WriteBodyAsync(answer.Body, isChunked))
        {
            connection.Abort();

            return false;
        }

        if (!keepsConnectionOpen)
        {
            await connection.CompleteWritesAsync(context.CancellationToken);
            await DrainBeforeCloseAsync(answer);
        }

        return keepsConnectionOpen;
    }

    /// <summary>
    /// Writes a refusal with <c>Connection: close</c>, and its body when it is
    /// <see cref="HttpAnswerBody.Bytes"/>, within <see cref="RefusalWriteDeadline"/>,
    /// and half-closes the connection; past the deadline the server gives up on it and leaves
    /// the close to the engine's dispose, never an abort (ADR-0006, section 5). Once the
    /// refusal is written, what the client still sends is drained (ADR-0024), unless the
    /// answer says <see cref="HttpAnswer.DrainsBeforeClose"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="answer">The refusal.</param>
    /// <returns><see langword="false"/>: the connection never stays open after a refusal.</returns>
    public async Task<bool> WriteRefusalAsync(HttpAnswer answer)
    {
        if (await TryWriteRefusalAsync(answer))
        {
            await DrainBeforeCloseAsync(answer);
        }

        return false;
    }

    // The lingering close (ADR-0024), unless the answer leaves what follows unread (ADR-0108, decision 7).
    private async Task DrainBeforeCloseAsync(HttpAnswer answer)
    {
        if (answer.DrainsBeforeClose)
        {
            await unreadRequestDrainer.DrainAsync();
        }
    }

    // An upgrade (ADR-0084, decision 1): the head as the answer gives it, its own Connection
    // field included, then the takeover owns the connection; no body, drain or close follows.
    private async Task WriteTakeoverAsync(HttpAnswer answer, IHttpConnectionTakeover takeover)
    {
        await connection.WriteAsync(ResponseHead(answer, null).ToBytes(), context.CancellationToken);
        await takeover.TakeOverAsync(connection, unreadRequestDrainer.Reader, context.CancellationToken);
    }

    private async Task<bool> TryWriteRefusalAsync(HttpAnswer answer)
    {
        using var deadline = new CancellationTokenSource(RefusalWriteDeadline, context.TimeProvider);
        using var deadlineOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            await connection.WriteAsync(ResponseHead(answer, "close").ToBytes(), deadlineOrExchange.Token);
            if (answer.Body is HttpAnswerBody.Bytes bytes)
            {
                await connection.WriteAsync(bytes.Content, deadlineOrExchange.Token);
            }

            await connection.CompleteWritesAsync(deadlineOrExchange.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note($"The {answer.Status.Code} was not written within its {RefusalWriteDeadline.TotalSeconds}-second write deadline; the connection is closed without it.");

            return false;
        }

        return true;
    }

    private static HttpResponseHead ResponseHead(HttpAnswer answer, string? connectionFieldValue)
    {
        var head = new HttpResponseHead(answer.Status);
        foreach (var field in answer.Fields)
        {
            head.AddField(field.Key, field.Value);
        }

        return head.AddField("Connection", connectionFieldValue);
    }

    // False when the body could not be sent whole and the connection must be aborted.
    private async Task<bool> WriteBodyAsync(HttpAnswerBody body, bool isChunked)
    {
        switch (body)
        {
            case HttpAnswerBody.ContentFile file:
                return await CopyFileAsync(HttpAnswerBody.ContentFileParts.Of(file));
            case HttpAnswerBody.ContentFileParts parts:
                return await CopyFileAsync(parts);
            case HttpAnswerBody.Bytes bytes:
                await connection.WriteAsync(bytes.Content, context.CancellationToken);
                break;
        }

        if (body is HttpAnswerBody.Streamed streamed)
        {
            await StreamAsync(streamed, isChunked);
        }

        return true;
    }

    // RFC 9112 section 7.1: each piece is one chunk - its size in hexadecimal, CRLF, the bytes, CRLF - and the
    // last chunk, size 0, then the CRLF that ends the empty trailer section. An empty piece is skipped, because
    // a chunk of size 0 would end the body.
    private async Task StreamAsync(HttpAnswerBody.Streamed streamed, bool isChunked)
    {
        await streamed.WriteAsync(isChunked ? WriteChunkAsync : connection.WriteAsync, context.CancellationToken);
        if (isChunked)
        {
            await connection.WriteAsync(LastChunk, context.CancellationToken);
        }
    }

    private async ValueTask WriteChunkAsync(ReadOnlyMemory<byte> piece, CancellationToken cancellationToken)
    {
        if (piece.IsEmpty)
        {
            return;
        }

        await connection.WriteAsync(Encoding.ASCII.GetBytes(piece.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n"), cancellationToken);
        await connection.WriteAsync(piece, cancellationToken);
        await connection.WriteAsync(ChunkEnd, cancellationToken);
    }

    private static readonly byte[] ChunkEnd = "\r\n"u8.ToArray();

    private static readonly byte[] LastChunk = "0\r\n\r\n"u8.ToArray();

    // After the head the Content-Length is promised, so a file that shrinks or cannot be
    // read any more can only be answered by aborting the connection; a reset tells curl the
    // transfer failed. A failure of the connection itself is not the file's and is rethrown.
    private async Task<bool> CopyFileAsync(HttpAnswerBody.ContentFileParts parts)
    {
        await using var body = new ConnectionWriteStream(connection);
        try
        {
            var copied = await parts.CopyToAsync(body, context.CancellationToken);
            if (copied < parts.FileByteCount)
            {
                context.Log.Note($"{parts.Mapping.Location} shrank to {copied} bytes while it was sent; the connection was aborted.");
            }

            return copied == parts.FileByteCount;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException && !body.HasFailedWrite)
        {
            context.Log.Note($"{parts.Mapping.Location} could not be read after the 200 head was sent ({failure.GetType().Name}: {failure.Message}); the connection was aborted.");

            return false;
        }
    }
}
