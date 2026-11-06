using System.Net;
using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// A connection the test writes the client's side of while the server runs: <see cref="Send"/>
/// queues bytes and <see cref="Close"/> half-closes the client's side. Reads throw
/// <see cref="IOException"/> under <see cref="ReadsFail"/>; writes throw <see cref="IOException"/>
/// under <see cref="WritesFail"/> and <see cref="OperationCanceledException"/> under
/// <see cref="WritesAreCancelled"/>.
/// </summary>
internal sealed class UnitTestXmppClientConnection : IConnection
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private readonly Lock gate = new();
    private readonly StringBuilder written = new();
    private ReadOnlyMemory<byte> current;

    public bool ReadsFail { get; init; }

    public bool WritesFail { get; init; }

    public bool WritesAreCancelled { get; init; }

    public int ReadsWaiting { get; private set; }

    public EndPoint LocalEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, 5222);

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
        if (ReadsFail)
        {
            throw new IOException("The connection was reset.");
        }

        while (current.IsEmpty)
        {
            ReadsWaiting++;
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
        if (WritesFail)
        {
            throw new IOException("The connection was reset.");
        }

        if (WritesAreCancelled)
        {
            throw new OperationCanceledException();
        }

        lock (gate)
        {
            written.Append(Encoding.UTF8.GetString(bytes.Span));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort() => Close();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
