using Surl.HttpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Writes the <c>text/event-stream</c> body a <c>tools/call</c> is answered with (ADR-0111 decisions 4.3 and
/// 4.8), by the WHATWG HTML "Server-sent events" section: while the call runs, a <c>: keep-alive</c> comment and
/// a blank line every <see cref="KeepAliveInterval" /> on the injected clock, which the client ignores; then one
/// event of the default type, its one <c>data:</c> line holding the JSON-RPC response, then the blank line that
/// dispatches it. No <c>event:</c> and no <c>id:</c> field is written. A cancelled call ends the stream with no
/// event.
/// </summary>
internal static class McpEventStream
{
    /// <summary>The media type of the stream.</summary>
    public const string MediaType = "text/event-stream";

    /// <summary>How long the stream stays silent while the call runs before a keep-alive comment is written.</summary>
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    private static readonly byte[] KeepAliveComment = ": keep-alive\n\n"u8.ToArray();

    private static readonly byte[] DataFieldPrefix = "data: "u8.ToArray();

    private static readonly byte[] EventEnd = "\n\n"u8.ToArray();

    /// <summary>Writes the stream as the call runs: a keep-alive comment each interval, then its event.</summary>
    /// <param name="call">The running call: its JSON-RPC response, or <see langword="null" /> when it was cancelled.</param>
    /// <param name="writeAsync">Hands each piece of the stream to the connection.</param>
    /// <param name="timeProvider">The clock the interval is measured on.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>A task that completes once the event, or nothing for a cancelled call, has been written.</returns>
    public static async Task WriteAsync(Task<byte[]?> call, HttpAnswerChunkWriter writeAsync, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        using CancellationTokenSource keepAliveTimers = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        while (await Task.WhenAny(call, Task.Delay(KeepAliveInterval, timeProvider, keepAliveTimers.Token)).ConfigureAwait(false) != call)
        {
            await writeAsync(KeepAliveComment, cancellationToken).ConfigureAwait(false);
        }

        await keepAliveTimers.CancelAsync().ConfigureAwait(false);
        await writeAsync(Write(await call.ConfigureAwait(false)), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the stream's event.</summary>
    /// <param name="response">The JSON-RPC response, on one line, or <see langword="null" /> when the call was cancelled.</param>
    /// <returns>The event, or nothing.</returns>
    public static byte[] Write(byte[]? response) =>
        response is null ? [] : [.. DataFieldPrefix, .. response, .. EventEnd];
}
