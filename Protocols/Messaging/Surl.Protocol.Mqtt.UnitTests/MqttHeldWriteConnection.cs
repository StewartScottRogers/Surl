using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// A connection whose client has already sent everything it will send, but whose first write -
/// the server's <c>CONNACK</c> - does not complete until the test calls <see cref="Release"/>: the
/// server has answered the client and not yet read what the client sent next. It opens nothing.
/// </summary>
internal sealed class MqttHeldWriteConnection(byte[] request) : IConnection
{
    private readonly InMemoryConnection client = new(RecordedFixture.Whole(request));
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public EndPoint LocalEndPoint => client.LocalEndPoint;

    public EndPoint RemoteEndPoint => client.RemoteEndPoint;

    public TlsSession? TlsSession => null;

    /// <summary>Lets the held write, and every later one, complete.</summary>
    public void Release() => released.TrySetResult();

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => client.ReadAsync(buffer, cancellationToken);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await released.Task.WaitAsync(cancellationToken);
        await client.WriteAsync(bytes, cancellationToken);
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => client.CompleteWritesAsync(cancellationToken);

    public void Abort() => client.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => client.DisposeAsync();
}
