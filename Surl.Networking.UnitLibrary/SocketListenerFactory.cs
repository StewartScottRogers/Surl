using System.Diagnostics.CodeAnalysis;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Starts the socket-backed listeners (ADR-0004, section 6): TCP connection listeners through
/// <see cref="TcpConnectionListener"/> and UDP datagram listeners through
/// <see cref="UdpDatagramListener"/>. It holds no state, so one instance serves every listen URL.
/// </summary>
public sealed class SocketListenerFactory : IListenerFactory
{
    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Delegates to TcpConnectionListener.StartAsync, which binds sockets; covered by the integration tests.")]
    public async ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        await TcpConnectionListener.StartAsync(listenUrl, cancellationToken);

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Delegates to UdpDatagramListener.StartAsync, which binds sockets; covered by the integration tests.")]
    public async ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        await UdpDatagramListener.StartAsync(listenUrl, cancellationToken);
}
