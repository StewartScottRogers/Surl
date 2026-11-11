using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// An <see cref="IConnection"/> a test feeds while the server runs - unlike
/// <see cref="InMemoryConnection"/>, whose script is fixed up front - so a hub connection can be
/// driven message by message. It records every byte written and lets a test wait for them.
/// </summary>
internal sealed class UnitTestDuplexConnection : IConnection
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly Channel<object> inbound = Channel.CreateUnbounded<object>();
    private readonly Lock gate = new();
    private readonly MemoryStream written = new();
    private TaskCompletionSource writtenChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ReadOnlyMemory<byte> current;
    private TaskCompletionSource writesReleased = Released();

    public TlsSession? TlsSession => null;

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

    /// <summary>Makes every write wait, until <see cref="ReleaseWrites"/> or its token, as a client that reads nothing.</summary>
    public void HoldWrites() => writesReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets held writes, and every later one, complete.</summary>
    public void ReleaseWrites() => writesReleased.TrySetResult();

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await writesReleased.Task.WaitAsync(cancellationToken);
        Record(bytes);
    }

    private void Record(ReadOnlyMemory<byte> bytes)
    {
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

    private static TaskCompletionSource Released()
    {
        var released = new TaskCompletionSource();
        released.SetResult();

        return released;
    }

    public void Abort()
    {
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
