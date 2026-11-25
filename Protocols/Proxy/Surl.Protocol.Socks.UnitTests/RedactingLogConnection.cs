using System.Buffers;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// curl's connection as the serving engine logs it: each read's bytes go to <c>log</c> through
/// <c>redactor</c>, which is shown every write once it has been sent. One lock keeps the redactor in one
/// order while the relay reads and writes at once.
/// </summary>
internal sealed class RedactingLogConnection(IConnection inner, IReceivedBytesRedactor redactor, IExchangeLog log) : IConnection
{
    private readonly Lock gate = new();
    private readonly ArrayBufferWriter<byte> output = new();

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        lock (gate)
        {
            output.Clear();
            redactor.Redact(buffer.Span[..read], output);
            log.BytesReceived(output.WrittenSpan);
        }

        return read;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await inner.WriteAsync(bytes, cancellationToken);
        lock (gate)
        {
            redactor.ObserveSent(bytes.Span);
        }
    }

    /// <summary>Logs whatever the redactor still holds back, as the engine does when the exchange ends.</summary>
    public void FlushLog()
    {
        lock (gate)
        {
            output.Clear();
            redactor.Flush(output);
            log.BytesReceived(output.WrittenSpan);
        }
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
