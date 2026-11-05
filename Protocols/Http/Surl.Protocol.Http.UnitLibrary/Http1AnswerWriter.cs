using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

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
    private readonly ContentStore contentStore;

    /// <summary>
    /// Creates a writer for one connection.
    /// </summary>
    /// <param name="connection">Where answers are written.</param>
    /// <param name="unreadRequestDrainer">Drains what the client still sends once the connection is half-closed.</param>
    /// <param name="context">The exchange: its clock, log and cancellation.</param>
    /// <param name="contentStore">Where the bytes of a content-store file are copied from.</param>
    public Http1AnswerWriter(IConnection connection, HttpUnreadRequestDrainer unreadRequestDrainer, ExchangeContext context, ContentStore contentStore)
    {
        this.connection = connection;
        this.unreadRequestDrainer = unreadRequestDrainer;
        this.context = context;
        this.contentStore = contentStore;
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
        if (answer.IsRefusal)
        {
            return await WriteRefusalAsync(answer);
        }

        var keepsConnectionOpen = HttpConnectionPersistence.KeepsConnectionOpen(head) && !answer.LeavesRequestBodyUnread;
        await connection.WriteAsync(ResponseHead(answer, HttpConnectionPersistence.ConnectionFieldValue(keepsConnectionOpen, head.Version)).ToBytes(), context.CancellationToken);

        if (!await WriteBodyAsync(answer.Body))
        {
            connection.Abort();

            return false;
        }

        if (!keepsConnectionOpen)
        {
            await connection.CompleteWritesAsync(context.CancellationToken);
            await unreadRequestDrainer.DrainAsync();
        }

        return keepsConnectionOpen;
    }

    /// <summary>
    /// Writes a refusal with <c>Connection: close</c>, within <see cref="RefusalWriteDeadline"/>,
    /// and half-closes the connection; past the deadline the server gives up on it and leaves
    /// the close to the engine's dispose, never an abort (ADR-0006, section 5). Once the
    /// refusal is written, what the client still sends is drained (ADR-0024).
    /// </summary>
    /// <param name="answer">The refusal.</param>
    /// <returns><see langword="false"/>: the connection never stays open after a refusal.</returns>
    public async Task<bool> WriteRefusalAsync(HttpAnswer answer)
    {
        if (await TryWriteRefusalAsync(answer))
        {
            await unreadRequestDrainer.DrainAsync();
        }

        return false;
    }

    private async Task<bool> TryWriteRefusalAsync(HttpAnswer answer)
    {
        using var deadline = new CancellationTokenSource(RefusalWriteDeadline, context.TimeProvider);
        using var deadlineOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            await connection.WriteAsync(ResponseHead(answer, "close").ToBytes(), deadlineOrExchange.Token);
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
    private async Task<bool> WriteBodyAsync(HttpAnswerBody body)
    {
        if (body is HttpAnswerBody.ContentFile file)
        {
            return await CopyFileAsync(file.Mapping, file.Range);
        }

        if (body is HttpAnswerBody.Bytes bytes)
        {
            await connection.WriteAsync(bytes.Content, context.CancellationToken);
        }

        return true;
    }

    // After the head the Content-Length is promised, so a file that shrinks or cannot be
    // read any more can only be answered by aborting the connection; a reset tells curl the
    // transfer failed. A failure of the connection itself is not the file's and is rethrown.
    private async Task<bool> CopyFileAsync(ContentPathMapping mapping, ContentByteRange range)
    {
        await using var body = new ConnectionWriteStream(connection);
        try
        {
            var copied = await contentStore.CopyFileBytesAsync(mapping, range, body, context.CancellationToken);
            if (copied < range.ByteCount)
            {
                context.Log.Note($"{mapping.Location} shrank to {copied} bytes while it was sent; the connection was aborted.");
            }

            return copied == range.ByteCount;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException && !body.HasFailedWrite)
        {
            context.Log.Note($"{mapping.Location} could not be read after the 200 head was sent ({failure.GetType().Name}: {failure.Message}); the connection was aborted.");

            return false;
        }
    }
}
