using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// A connection whose client has already sent everything it will send, but whose first write -
/// the broker's <c>CONNACK</c> - does not complete until the test calls <see cref="Release"/>: the
/// broker has answered the client and not yet read what the client sent next. It opens nothing.
/// </summary>
internal sealed class Mqtt5HeldWriteConnection(params byte[][] packets) : IConnection
{
    private readonly InMemoryConnection client = Mqtt5BrokerExchange.ConnectionSending(staysOpen: false, packets);
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
