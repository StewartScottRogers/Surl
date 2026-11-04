using System.Threading.Channels;

namespace Surl.Networking;

/// <summary>
/// One end of an in-memory, full-duplex byte pipe, so two <see cref="System.Net.Security.SslStream"/>s
/// can shake hands in a fast test without a socket. What one end writes, the other reads.
/// </summary>
internal sealed class InMemoryDuplexStream : Stream
{
    private readonly Channel<byte[]> inbound;
    private readonly Channel<byte[]> outbound;
    private readonly CancellationTokenSource disposal = new();
    private ReadOnlyMemory<byte> unread;

    private InMemoryDuplexStream(Channel<byte[]> inbound, Channel<byte[]> outbound)
    {
        this.inbound = inbound;
        this.outbound = outbound;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public int DisposeCount { get; private set; }

    public static (InMemoryDuplexStream Server, InMemoryDuplexStream Client) CreatePair()
    {
        var toServer = Channel.CreateUnbounded<byte[]>();
        var toClient = Channel.CreateUnbounded<byte[]>();

        return (new InMemoryDuplexStream(toServer, toClient), new InMemoryDuplexStream(toClient, toServer));
    }

    /// <summary>
    /// Ends this end's writes: the other end reads 0 once it has read everything before.
    /// </summary>
    public void CompleteWrites() => outbound.Writer.TryComplete();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposal.Token);

        try
        {
            while (unread.IsEmpty)
            {
                if (!await inbound.Reader.WaitToReadAsync(linked.Token))
                {
                    return 0;
                }

                inbound.Reader.TryRead(out var chunk);
                unread = chunk;
            }
        }
        catch (OperationCanceledException) when (disposal.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(InMemoryDuplexStream));
        }

        var count = Math.Min(buffer.Length, unread.Length);
        unread[..count].CopyTo(buffer);
        unread = unread[count..];

        return count;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposal.IsCancellationRequested, this);

        if (!outbound.Writer.TryWrite(buffer.ToArray()))
        {
            throw new IOException("This end's writes were completed.");
        }

        return ValueTask.CompletedTask;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    // SslStream sends a handshake-failure alert with a synchronous write; the pipe never blocks.
    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCount++;
            outbound.Writer.TryComplete();
            disposal.Cancel();
        }

        base.Dispose(disposing);
    }
}
