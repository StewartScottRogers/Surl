using System.Net;
using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>
/// A connection the test writes the client's side of while the server runs: <see cref="Send"/>
/// queues bytes, <see cref="Close"/> half-closes the client's side, and a write fails with
/// <see cref="IOException"/> once <see cref="FailWritesAfter"/> writes have succeeded.
/// </summary>
internal sealed class UnitTestStompClientConnection : IConnection
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private readonly Lock gate = new();
    private readonly StringBuilder written = new();
    private ReadOnlyMemory<byte> current;
    private int writes;

    public int FailWritesAfter { get; init; } = int.MaxValue;

    /// <summary>Whether every write waits until it is cancelled, as a transport that takes nothing.</summary>
    public bool HoldsWrites { get; init; }

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 61613);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    public string Written
    {
        get
        {
            lock (gate)
            {
                return written.ToString();
            }
        }
    }

    public void Send(string text) => inbound.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

    public void Close() => inbound.Writer.TryComplete();

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (current.IsEmpty)
        {
            if (!await inbound.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }

            current = await inbound.Reader.ReadAsync(cancellationToken);
        }

        var count = Math.Min(buffer.Length, current.Length);
        current[..count].CopyTo(buffer);
        current = current[count..];
        return count;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HoldsWrites)
        {
            // A transport that never takes the bytes: the write ends only when it is cancelled.
            return new ValueTask(Task.Delay(Timeout.Infinite, cancellationToken));
        }

        lock (gate)
        {
            if (writes++ >= FailWritesAfter)
            {
                throw new IOException("The connection was reset.");
            }

            written.Append(Encoding.UTF8.GetString(bytes.Span));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort() => Close();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
