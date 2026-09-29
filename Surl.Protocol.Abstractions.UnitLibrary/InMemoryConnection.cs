using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IConnection"/> that replays an inbound byte script and records every byte
/// written, so a protocol server is tested with no network (ADR-0004, section 7). It opens
/// nothing and constructs no transport type.
/// </summary>
/// <remarks>
/// Each read returns bytes from the current chunk only, never joining two chunks, so a test
/// controls how a request is split across reads. Empty chunks are skipped, because a read of
/// 0 bytes means the peer half-closed. It is meant for one test at a time and is not safe for
/// concurrent calls.
/// </remarks>
public sealed class InMemoryConnection : IConnection
{
    private readonly ReadOnlyMemory<byte>[] inboundChunks;
    private readonly bool peerHalfClosesWhenExhausted;
    private readonly MemoryStream writtenBytes = new();
    private readonly TaskCompletionSource aborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int chunkIndex;
    private int chunkOffset;

    /// <summary>
    /// Creates a connection that replays <paramref name="inboundChunks"/>.
    /// </summary>
    /// <param name="inboundChunks">The bytes the client sends, in order, one read's worth per chunk at most.</param>
    /// <param name="localEndPoint">The local endpoint; 127.0.0.1:80 when <see langword="null"/>.</param>
    /// <param name="remoteEndPoint">The client's endpoint; 127.0.0.1:50000 when <see langword="null"/>.</param>
    /// <param name="peerHalfClosesWhenExhausted">
    /// Whether the client half-closes once the script is exhausted, so reads return 0; when
    /// <see langword="false"/>, a read after the script waits until it is cancelled or the
    /// connection is aborted.
    /// </param>
    public InMemoryConnection(
        IEnumerable<ReadOnlyMemory<byte>> inboundChunks,
        EndPoint? localEndPoint = null,
        EndPoint? remoteEndPoint = null,
        bool peerHalfClosesWhenExhausted = true)
    {
        ArgumentNullException.ThrowIfNull(inboundChunks);

        this.inboundChunks = inboundChunks.Where(chunk => !chunk.IsEmpty).ToArray();
        LocalEndPoint = localEndPoint ?? new IPEndPoint(IPAddress.Loopback, 80);
        RemoteEndPoint = remoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 50000);
        this.peerHalfClosesWhenExhausted = peerHalfClosesWhenExhausted;
    }

    /// <inheritdoc/>
    public EndPoint LocalEndPoint { get; }

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// A copy of every byte written, in order.
    /// </summary>
    public byte[] WrittenBytes => writtenBytes.ToArray();

    /// <summary>
    /// Whether the server half-closed the connection with <see cref="CompleteWritesAsync"/>,
    /// or disposed it without aborting it first.
    /// </summary>
    public bool WritesCompleted { get; private set; }

    /// <summary>
    /// Whether <see cref="Abort"/> was called.
    /// </summary>
    public bool Aborted => aborted.Task.IsCompleted;

    /// <summary>
    /// Whether <see cref="DisposeAsync"/> was called.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        if (buffer.IsEmpty)
        {
            throw new ArgumentException("A read needs a buffer of at least one byte.", nameof(buffer));
        }

        if (chunkIndex < inboundChunks.Length)
        {
            return CopyFromCurrentChunk(buffer.Span);
        }

        return peerHalfClosesWhenExhausted ? 0 : await WaitForCancellationOrAbortAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        if (WritesCompleted)
        {
            throw new InvalidOperationException("The connection was half-closed; nothing more can be written.");
        }

        writtenBytes.Write(bytes.Span);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        WritesCompleted = true;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Abort() => aborted.TrySetResult();

    /// <summary>
    /// Closes the connection: completes writes unless it was aborted. Calling it twice is harmless.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        WritesCompleted |= !Aborted;
        Disposed = true;

        return ValueTask.CompletedTask;
    }

    private int CopyFromCurrentChunk(Span<byte> destination)
    {
        var remaining = inboundChunks[chunkIndex].Span[chunkOffset..];
        var count = Math.Min(remaining.Length, destination.Length);

        remaining[..count].CopyTo(destination);
        chunkOffset += count;

        if (chunkOffset == inboundChunks[chunkIndex].Length)
        {
            chunkIndex++;
            chunkOffset = 0;
        }

        return count;
    }

    private async Task<int> WaitForCancellationOrAbortAsync(CancellationToken cancellationToken)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using (cancellationToken.Register(() => cancelled.TrySetResult()))
        {
            await Task.WhenAny(cancelled.Task, aborted.Task);
        }

        ThrowIfAborted();

        throw new OperationCanceledException(cancellationToken);
    }

    private void ThrowIfUnusable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Disposed, this);
        ThrowIfAborted();
    }

    private void ThrowIfAborted()
    {
        if (Aborted)
        {
            throw new IOException("The connection was aborted.");
        }
    }
}
