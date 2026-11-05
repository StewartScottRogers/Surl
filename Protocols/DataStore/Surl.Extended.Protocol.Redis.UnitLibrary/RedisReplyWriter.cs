using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Writes one connection's bytes: the replies to its own commands, and the pub/sub messages other
/// connections publish to it (ADR-0106 decision 1), one write at a time so neither cuts into the
/// other. A message is written without the publisher waiting for it; once more than the limit of
/// a subscriber's messages are unsent, or a message cannot be written, the connection is closed
/// with no more bytes (ADR-0106 decision 6, Redis's <c>client-output-buffer-limit pubsub</c>).
/// </summary>
internal sealed class RedisReplyWriter
{
    private readonly IConnection connection;
    private readonly long maxUnsentMessageBytes;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource closing;
    private long unsentMessageBytes;

    /// <summary>Creates the writer of <paramref name="connection"/>.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="maxUnsentMessageBytes">The most bytes of messages unsent before the connection is closed.</param>
    /// <param name="exchangeCancellation">Ends the exchange.</param>
    public RedisReplyWriter(IConnection connection, long maxUnsentMessageBytes, CancellationToken exchangeCancellation)
    {
        this.connection = connection;
        this.maxUnsentMessageBytes = maxUnsentMessageBytes;
        closing = CancellationTokenSource.CreateLinkedTokenSource(exchangeCancellation);
    }

    /// <summary>Cancelled when the exchange ends or the writer closes the connection; every read and write uses it.</summary>
    public CancellationToken Token => closing.Token;

    /// <summary>Whether the writer closed the connection: too many unsent messages, or one could not be written.</summary>
    public bool IsClosedByWriter { get; private set; }

    /// <summary>Writes <paramref name="bytes"/> after any write already under way.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A task that completes when they are written.</returns>
    public async ValueTask WriteAsync(byte[] bytes)
    {
        await gate.WaitAsync(Token);
        try
        {
            await connection.WriteAsync(bytes, Token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Half-closes the connection after any write already under way.</summary>
    /// <returns>A task that completes when the connection is half-closed.</returns>
    public async ValueTask CompleteWritesAsync()
    {
        await gate.WaitAsync(Token);
        try
        {
            await connection.CompleteWritesAsync(Token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Starts writing the message <paramref name="bytes"/> without waiting for it, or closes the
    /// connection when that would leave more than the limit of messages unsent.
    /// </summary>
    /// <param name="bytes">The message's bytes.</param>
    public void Deliver(byte[] bytes)
    {
        if (Interlocked.Add(ref unsentMessageBytes, bytes.Length) > maxUnsentMessageBytes)
        {
            Interlocked.Add(ref unsentMessageBytes, -bytes.Length);
            Close();
            return;
        }

        _ = DeliverAsync(bytes);
    }

    private async Task DeliverAsync(byte[] bytes)
    {
        try
        {
            await WriteAsync(bytes);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or InvalidOperationException or ObjectDisposedException)
        {
            Close();
        }
        finally
        {
            Interlocked.Add(ref unsentMessageBytes, -bytes.Length);
        }
    }

    private void Close()
    {
        IsClosedByWriter = true;
        closing.Cancel();
    }
}
