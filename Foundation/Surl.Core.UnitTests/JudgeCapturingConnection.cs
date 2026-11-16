using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// An in-memory connection that keeps the <see cref="ITlsLoginJudge"/> the engine hands it.
/// </summary>
internal sealed class JudgeCapturingConnection : IConnection
{
    private readonly InMemoryConnection inner = new([]);

    public ITlsLoginJudge? Judge { get; private set; }

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public void UseTlsLoginJudge(ITlsLoginJudge judge) => Judge = judge;

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
