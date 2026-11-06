using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// An <see cref="IConnection"/> a test feeds while the server runs - unlike
/// <see cref="InMemoryConnection"/>, whose script is fixed up front - so a session can be driven
/// message by message. It records every byte written, lets a test wait for them, and can hold the
/// server's writes back to stand in for a client that reads slowly.
/// </summary>
internal sealed class UnitTestDuplexConnection(TlsSession? tlsSession = null) : IConnection
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly Channel<object> inbound = Channel.CreateUnbounded<object>();
    private readonly Lock gate = new();
    private readonly MemoryStream written = new();
    private TaskCompletionSource writtenChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource writesReleased = CompletedSource();
    private ReadOnlyMemory<byte> current;
    private volatile bool writesFail;

    public TlsSession? TlsSession { get; } = tlsSession;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 5000);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public bool WritesCompleted { get; private set; }

    public byte[] WrittenBytes
    {
        get
        {
            lock (gate)
            {
                return written.ToArray();
            }
        }
    }

    /// <summary>Delivers <paramref name="bytes"/> to the server's next read.</summary>
    public void Send(byte[] bytes) => inbound.Writer.TryWrite(bytes);

    /// <summary>Makes the server's next read, after what was sent, throw <paramref name="failure"/>.</summary>
    public void FailNextRead(Exception failure) => inbound.Writer.TryWrite(failure);

    /// <summary>Ends the client's side: once what was sent is read, reads return 0.</summary>
    public void HalfClose() => inbound.Writer.TryComplete();

    /// <summary>Holds every write from now on until <see cref="ReleaseWrites"/>.</summary>
    public void HoldWrites() => writesReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets held writes, and later ones, through.</summary>
    public void ReleaseWrites() => writesReleased.TrySetResult();

    /// <summary>Waits until what the server wrote satisfies <paramref name="condition"/>.</summary>
    public async Task<byte[]> WaitForWrittenAsync(Func<byte[], bool> condition)
    {
        while (true)
        {
            Task changed;
            lock (gate)
            {
                var bytes = written.ToArray();
                if (condition(bytes))
                {
                    return bytes;
                }

                changed = writtenChanged.Task;
            }

            await changed.WaitAsync(WaitLimit);
        }
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (current.IsEmpty)
        {
            if (!await inbound.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }

            var item = await inbound.Reader.ReadAsync(cancellationToken);
            if (item is Exception failure)
            {
                throw failure;
            }

            current = (byte[])item;
        }

        var count = Math.Min(buffer.Length, current.Length);
        current[..count].CopyTo(buffer);
        current = current[count..];

        return count;
    }

    /// <summary>Makes every write from now on throw <see cref="IOException"/>, as a reset connection does.</summary>
    public void FailWrites() => writesFail = true;

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await writesReleased.Task.WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (writesFail)
        {
            throw new IOException("The connection was reset.");
        }

        lock (gate)
        {
            written.Write(bytes.Span);
            writtenChanged.TrySetResult();
            writtenChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        WritesCompleted = true;

        return ValueTask.CompletedTask;
    }

    public void Abort()
    {
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource();
        source.SetResult();

        return source;
    }
}
