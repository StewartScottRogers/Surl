using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// Reads and throws away what a client still sends after the server shut down its sending
/// side, until the client closes or <see cref="MaxLingerTime"/> passes, so unread client bytes
/// do not turn the close into a TCP reset that could cost the client the server's last bytes
/// (ADR-0071 decision 5, as ADR-0024's lingering close).
/// </summary>
/// <remarks>
/// The linger also ends when the client resets the connection. Only the end of the exchange
/// itself (its cancellation) escapes it.
/// </remarks>
internal sealed class WebSocketLingeringClose
{
    /// <summary>
    /// The longest one linger waits: one second, ADR-0059's farewell window.
    /// </summary>
    public static readonly TimeSpan MaxLingerTime = TimeSpan.FromSeconds(1);

    private const int LingerBufferBytes = 8192;

    private readonly HttpConnectionReader reader;
    private readonly ExchangeContext context;
    private readonly byte[] lingerBuffer = new byte[LingerBufferBytes];

    /// <summary>
    /// Creates a linger that reads through <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">The connection's reader, so bytes it already buffered are read first.</param>
    /// <param name="context">The exchange: its clock, log and cancellation.</param>
    public WebSocketLingeringClose(HttpConnectionReader reader, ExchangeContext context)
    {
        this.reader = reader;
        this.context = context;
    }

    /// <summary>
    /// Reads and discards what the client still sends until it closes, and notes in the
    /// exchange log a linger that the time limit or a reset ended.
    /// </summary>
    /// <param name="endingToken">
    /// What ends the linger at once: the exchange's cancellation, or - for the farewell after a
    /// limit, when that is already cancelled - its shutdown (ADR-0059).
    /// </param>
    /// <returns>A task that completes when the linger has ended.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="endingToken"/> was cancelled.</exception>
    public async Task LingerAsync(CancellationToken endingToken)
    {
        using var deadline = new CancellationTokenSource(MaxLingerTime, context.TimeProvider);
        using var deadlineOrExchange = CancellationTokenSource.CreateLinkedTokenSource(endingToken, deadline.Token);
        try
        {
            while (await reader.ReadAsync(lingerBuffer, deadlineOrExchange.Token) > 0)
            {
            }
        }
        catch (OperationCanceledException) when (!endingToken.IsCancellationRequested)
        {
            context.Log.Note($"Stopped reading what the client still sent at the {MaxLingerTime.TotalSeconds}-second lingering close.");
        }
        catch (IOException failure)
        {
            context.Log.Note($"The connection failed during the lingering close ({failure.Message}).");
        }
    }
}
