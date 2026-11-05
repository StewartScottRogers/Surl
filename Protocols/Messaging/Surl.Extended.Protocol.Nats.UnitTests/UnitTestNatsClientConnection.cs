using System.Net;
using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>
/// A connection the test writes the client's side of while the server runs: <see cref="Send"/>
/// queues bytes, <see cref="Close"/> half-closes the client's side, and a write fails with
/// <see cref="IOException"/> once <see cref="FailWritesAfter"/> writes have succeeded.
/// </summary>
internal sealed class UnitTestNatsClientConnection : IConnection
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private readonly Lock gate = new();
    private readonly StringBuilder written = new();
    private ReadOnlyMemory<byte> current;
    private int writes;
    private int attempts;
    private bool holding = true;

    public int FailWritesAfter { get; init; } = int.MaxValue;

    /// <summary>Writes after this many attempts wait, taking nothing, until they are cancelled.</summary>
    public int HoldWritesAfter { get; init; } = int.MaxValue;

    /// <summary>Whether writes still wait after a held one was cancelled.</summary>
    public bool KeepsHoldingAfterCancel { get; init; }

    /// <summary>How many writes the server has begun, held ones included.</summary>
    public int WriteAttempts => Volatile.Read(ref attempts);

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 4222);

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

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Increment(ref attempts) > HoldWritesAfter && Volatile.Read(ref holding))
        {
            await HoldAsync(cancellationToken);
        }

        lock (gate)
        {
            if (writes++ >= FailWritesAfter)
            {
                throw new IOException("The connection was reset.");
            }

            written.Append(Encoding.UTF8.GetString(bytes.Span));
        }
    }

    // A transport that takes nothing until the write is cancelled; then it takes writes again
    // unless it keeps holding.
    private async Task HoldAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Volatile.Write(ref holding, KeepsHoldingAfterCancel);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort() => Close();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
