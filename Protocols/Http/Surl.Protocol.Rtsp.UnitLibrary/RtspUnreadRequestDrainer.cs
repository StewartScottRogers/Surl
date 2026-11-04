using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// Reads and throws away what a client still sends after the server half-closed the
/// connection with a refusal, so the unread bytes do not turn the close into a TCP reset that
/// destroys the refusal - the lingering close the HTTP server does (ADR-0024), which ADR-0074
/// decision 2 has a closing RTSP refusal do too.
/// </summary>
/// <remarks>
/// The drain ends when the client half-closes, when <see cref="MaxDrainBytes"/> have been
/// read, when <see cref="MaxDrainTime"/> has passed on the exchange's
/// <see cref="ExchangeContext.TimeProvider"/>, or when the client resets the connection.
/// Only the end of the exchange itself (its cancellation) escapes it.
/// </remarks>
internal sealed class RtspUnreadRequestDrainer
{
    /// <summary>
    /// The most bytes one drain reads: 1 MiB.
    /// </summary>
    public const int MaxDrainBytes = 1024 * 1024;

    /// <summary>
    /// The longest one drain waits: one second, the budget ADR-0006, section 5 gives a refusal.
    /// </summary>
    public static readonly TimeSpan MaxDrainTime = TimeSpan.FromSeconds(1);

    private const int DrainBufferBytes = 8192;

    private readonly HttpConnectionReader reader;
    private readonly ExchangeContext context;
    private readonly byte[] drainBuffer = new byte[DrainBufferBytes];

    /// <summary>
    /// Creates a drainer that reads through <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">The connection's reader, so bytes it already buffered are drained first.</param>
    /// <param name="context">The exchange: its clock, log and cancellation.</param>
    public RtspUnreadRequestDrainer(HttpConnectionReader reader, ExchangeContext context)
    {
        this.reader = reader;
        this.context = context;
    }

    /// <summary>
    /// Reads and discards what the client still sends, within the drain's bounds, and notes
    /// in the exchange log a drain that a bound or a reset ended.
    /// </summary>
    /// <returns>A task that completes when the drain has ended.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled.</exception>
    public async Task DrainAsync()
    {
        using var deadline = new CancellationTokenSource(MaxDrainTime, context.TimeProvider);
        using var deadlineOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            if (!await ReadUntilClientClosesAsync(deadlineOrExchange.Token))
            {
                context.Log.Note($"Stopped draining the unread request bytes at the {MaxDrainBytes}-byte drain limit.");
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note($"Stopped draining the unread request bytes at the {MaxDrainTime.TotalSeconds}-second drain limit.");
        }
        catch (IOException failure)
        {
            context.Log.Note($"The connection failed while its unread request bytes were drained ({failure.Message}).");
        }
    }

    // True once the client half-closed; false when the byte limit was reached first.
    private async Task<bool> ReadUntilClientClosesAsync(CancellationToken cancellationToken)
    {
        var drained = 0;
        while (drained < MaxDrainBytes)
        {
            var read = await reader.ReadAsync(drainBuffer.AsMemory(0, Math.Min(drainBuffer.Length, MaxDrainBytes - drained)), cancellationToken);
            if (read == 0)
            {
                return true;
            }

            drained += read;
        }

        return false;
    }
}
