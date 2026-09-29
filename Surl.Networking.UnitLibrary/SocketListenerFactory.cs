using System.Diagnostics.CodeAnalysis;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Starts the socket-backed listeners (ADR-0004, section 6): TCP connection listeners through
/// <see cref="TcpConnectionListener"/> and UDP datagram listeners through
/// <see cref="UdpDatagramListener"/>. It holds only the process's TLS settings, so one
/// instance serves every listen URL.
/// </summary>
public sealed class SocketListenerFactory : IListenerFactory
{
    private readonly ServerTlsSettings? tlsSettings;

    /// <summary>
    /// Creates a factory whose connections cannot be secured.
    /// </summary>
    public SocketListenerFactory()
        : this(null)
    {
    }

    /// <summary>
    /// Creates a factory whose connection listeners secure connections with
    /// <paramref name="tlsSettings"/> when upgraded (ADR-0010).
    /// </summary>
    /// <param name="tlsSettings">The process's TLS settings, or <see langword="null"/> for none.</param>
    public SocketListenerFactory(ServerTlsSettings? tlsSettings) => this.tlsSettings = tlsSettings;

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Delegates to TcpConnectionListener.StartAsync, which binds sockets; covered by the integration tests.")]
    public async ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        await TcpConnectionListener.StartAsync(listenUrl, tlsSettings, cancellationToken);

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Delegates to UdpDatagramListener.StartAsync, which binds sockets; covered by the integration tests.")]
    public async ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        await UdpDatagramListener.StartAsync(listenUrl, cancellationToken);
}
