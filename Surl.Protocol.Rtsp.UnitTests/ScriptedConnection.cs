using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// An in-memory <see cref="IConnection"/> whose client sends each chunk only once its condition
/// holds - checked after every write and on <see cref="Recheck"/> - so a test can send a request
/// while the server is streaming, after a chosen frame. A read waiting for a chunk is completed
/// inside the write that releases it. Once every chunk is read, the client half-closes, or resets the connection.
/// </summary>
internal sealed class ScriptedConnection(params ScriptedConnection.Chunk[] chunks) : IConnection
{
    private readonly MemoryStream writtenBytes = new();
    private int chunkIndex;
    private int chunkOffset;
    private bool chunkReleased;
    private TaskCompletionSource? releaseSignal;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 18554);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    public byte[] WrittenBytes => writtenBytes.ToArray();

    /// <summary>
    /// Whether the client resets the connection, rather than half-closing it, once every chunk is read.
    /// </summary>
    public bool ResetWhenDone { get; init; }

    /// <summary>
    /// A chunk sent as soon as the connection is read.
    /// </summary>
    public static Chunk Immediately(string text) => new(RtspServerHarness.Ascii(text), _ => true);

    /// <summary>
    /// A chunk sent once <paramref name="isReleased"/> holds for the bytes written so far.
    /// </summary>
    public static Chunk When(string text, Func<byte[], bool> isReleased) => new(RtspServerHarness.Ascii(text), isReleased);

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (chunkIndex < chunks.Length)
        {
            chunkReleased |= chunks[chunkIndex].IsReleased(WrittenBytes);
            if (chunkReleased)
            {
                return CopyFromCurrentChunk(buffer.Span);
            }

            releaseSignal = new TaskCompletionSource();
            using (cancellationToken.Register(() => releaseSignal.TrySetCanceled(cancellationToken)))
            {
                await releaseSignal.Task;
            }
        }

        return ResetWhenDone ? throw new IOException("The connection was reset.") : 0;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        writtenBytes.Write(bytes.Span);
        Recheck();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Lets a waiting read check its chunk's condition again, after the test changed what it depends on.
    /// </summary>
    public void Recheck() => releaseSignal?.TrySetResult();

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort()
    {
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("This connection never upgrades.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private int CopyFromCurrentChunk(Span<byte> destination)
    {
        var remaining = chunks[chunkIndex].Bytes.AsSpan(chunkOffset);
        var count = Math.Min(remaining.Length, destination.Length);
        remaining[..count].CopyTo(destination);
        chunkOffset += count;
        if (chunkOffset == chunks[chunkIndex].Bytes.Length)
        {
            chunkIndex++;
            chunkOffset = 0;
            chunkReleased = false;
        }

        return count;
    }

    /// <summary>
    /// What the client sends, and when.
    /// </summary>
    public sealed record Chunk(byte[] Bytes, Func<byte[], bool> IsReleased);
}
