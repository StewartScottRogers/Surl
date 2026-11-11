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
    private readonly TimeProvider timeProvider;

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
    public SocketListenerFactory(ServerTlsSettings? tlsSettings)
        : this(tlsSettings, null)
    {
    }

    /// <summary>
    /// Creates a factory as <see cref="SocketListenerFactory(ServerTlsSettings?)"/> does, whose connections
    /// time their peek waits and closes by <paramref name="timeProvider"/> (ADR-0124, decision 6).
    /// </summary>
    /// <param name="tlsSettings">The process's TLS settings, or <see langword="null"/> for none.</param>
    /// <param name="timeProvider">The instance's clock; <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public SocketListenerFactory(ServerTlsSettings? tlsSettings, TimeProvider? timeProvider)
    {
        this.tlsSettings = tlsSettings;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A listen URL with <see cref="ListenUrl.UnixSocket"/> set listens on that socket through
    /// <see cref="UnixSocketConnectionListener"/> (ADR-0094, decision 6); any other on TCP.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "Delegates to the listeners' StartAsync, which bind sockets; covered by SocketListenerFactoryTests and UnixSocketConnectionListenerTests.")]
    public async ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        listenUrl.UnixSocket is null
            ? await TcpConnectionListener.StartAsync(listenUrl, tlsSettings, cancellationToken, timeProvider)
            : await UnixSocketConnectionListener.StartAsync(listenUrl, tlsSettings, cancellationToken, timeProvider);

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Delegates to UdpDatagramListener.StartAsync, which binds sockets; covered by the integration tests.")]
    public async ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        await UdpDatagramListener.StartAsync(listenUrl, cancellationToken);

    /// <summary>
    /// Binds the TCP and UDP listeners with <see cref="SharedPortListenerPair"/>, which retries a
    /// port-0 pair whose UDP port is taken (ADR-0082, decision 6).
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="cancellationToken">Cuts the start off.</param>
    /// <returns>Both listeners, once both are bound.</returns>
    [ExcludeFromCodeCoverage(Justification = "Delegates to SharedPortListenerPair.StartAsync, which binds sockets; covered by its integration tests.")]
    public async ValueTask<SharedPortListeners> StartSharedPortListenersAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        var pair = await SharedPortListenerPair.StartAsync(
            listenUrl, StartConnectionListenerAsync, StartDatagramListenerAsync, cancellationToken);
        return new SharedPortListeners(pair.ConnectionListener, pair.DatagramListener);
    }
}
