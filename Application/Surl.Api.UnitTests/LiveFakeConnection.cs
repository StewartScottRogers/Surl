using System.Net;
using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A plaintext connection with no network behind it whose client stays open: the server reads only what the test
/// hands over through <see cref="Send"/>, one command at a time, and <see cref="WaitForOutputAsync"/> waits until
/// the server has written a given text. <see cref="Close"/> half-closes the client.
/// </summary>
internal sealed class LiveFakeConnection : IConnection
{
    private readonly Channel<byte[]> clientBytes = Channel.CreateUnbounded<byte[]>();
    private readonly StringBuilder written = new();
    private readonly List<TaskCompletionSource> waiters = [];
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private byte[] pending = [];

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, FakeListenerFactory.BoundPort);

    public EndPoint RemoteEndPoint => FakeConnection.Client;

    public TlsSession? TlsSession => null;

    /// <summary>Completes once the connection has been disposed.</summary>
    public Task Disposed => disposed.Task;

    /// <summary>Hands the server <paramref name="text"/> as what the client sent next.</summary>
    public void Send(string text) => clientBytes.Writer.TryWrite(Encoding.ASCII.GetBytes(text));

    /// <summary>Ends what the client sends, as a half-close does.</summary>
    public void Close() => clientBytes.Writer.TryComplete();

    /// <summary>Everything the server wrote so far.</summary>
    public string WrittenToClient
    {
        get
        {
            lock (written)
            {
                return written.ToString();
            }
        }
    }

    /// <summary>
    /// Completes once what the server wrote, counted from <paramref name="fromOffset"/>, holds <paramref name="text"/>;
    /// returns what it wrote from there to the end of that text.
    /// </summary>
    public async Task<string> WaitForOutputAsync(string text, int fromOffset, TimeSpan timeLimit, CancellationToken cancellationToken)
    {
        while (true)
        {
            TaskCompletionSource seen;
            lock (written)
            {
                var all = written.ToString();
                var index = all.IndexOf(text, fromOffset, StringComparison.Ordinal);
                if (index >= 0)
                {
                    return all[fromOffset..(index + text.Length)];
                }

                seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                waiters.Add(seen);
            }

            await seen.Task.WaitAsync(timeLimit, cancellationToken);
        }
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (pending.Length == 0)
        {
            if (!await clientBytes.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }

            pending = await clientBytes.Reader.ReadAsync(cancellationToken);
        }

        var count = Math.Min(buffer.Length, pending.Length);
        pending.AsMemory(0, count).CopyTo(buffer);
        pending = pending[count..];
        return count;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        List<TaskCompletionSource> toWake;
        lock (written)
        {
            written.Append(Encoding.ASCII.GetString(bytes.Span));
            toWake = [.. waiters];
            waiters.Clear();
        }

        toWake.ForEach(seen => seen.TrySetResult());
        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort()
    {
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The fake connection carries no TLS.");

    public ValueTask DisposeAsync()
    {
        disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
