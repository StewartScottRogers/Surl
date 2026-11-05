using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// A TCP connection listener and a UDP datagram listener for one listen URL, bound to the same
/// addresses and the same port: an <c>https</c> listen URL serving HTTP/3 over QUIC beside
/// HTTP/1.x and HTTP/2 (ADR-0082, decisions 5 and 6).
/// </summary>
/// <remarks>
/// The TCP listener binds first and the UDP listener then binds the port it got. When the listen
/// URL asks for port 0 and that UDP port is taken, both are released and the pair is tried again,
/// up to <see cref="MaxBindAttempts"/> times in all; any other UDP bind failure, or a taken UDP
/// port with a fixed port, releases the TCP listener and ends the start as a TCP bind failure
/// does. QUIC connections ride the UDP listener's flows, one per remote endpoint, and count
/// against the connection limits in the serving engine like any flow (ADR-0082, decision 5).
/// </remarks>
public sealed class SharedPortListenerPair : IAsyncDisposable
{
    /// <summary>
    /// How many times, at most, a port-0 pair is bound before a taken UDP port ends the start.
    /// </summary>
    public const int MaxBindAttempts = 10;

    private SharedPortListenerPair(IConnectionListener connectionListener, IDatagramListener datagramListener)
    {
        ConnectionListener = connectionListener;
        DatagramListener = datagramListener;
    }

    /// <summary>
    /// The TCP listener; its listen URL carries the shared port.
    /// </summary>
    public IConnectionListener ConnectionListener { get; }

    /// <summary>
    /// The UDP listener, bound to the same addresses and port as <see cref="ConnectionListener"/>;
    /// its listen URL names that port as the port asked for.
    /// </summary>
    public IDatagramListener DatagramListener { get; }

    /// <summary>
    /// Binds a <see cref="TcpConnectionListener"/> on every address <paramref name="listenUrl"/>
    /// names, then a <see cref="UdpDatagramListener"/> on the same addresses and port.
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="tlsSettings">The TLS settings the TCP listener's connections are secured with, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cuts the host-name resolution off.</param>
    /// <returns>The pair, once both are bound.</returns>
    /// <exception cref="ListenerBindException">
    /// The host did not resolve, or an address or the shared port could not be bound; nothing stays bound.
    /// </exception>
    // Passes SocketListenerFactory's TcpConnectionListener and UdpDatagramListener starts to the tested overload.
    [ExcludeFromCodeCoverage(Justification = "Binds sockets; covered by the integration tests.")]
    public static ValueTask<SharedPortListenerPair> StartAsync(
        ListenUrl listenUrl, ServerTlsSettings? tlsSettings, CancellationToken cancellationToken)
    {
        var factory = new SocketListenerFactory(tlsSettings);

        return StartAsync(listenUrl, factory.StartConnectionListenerAsync, factory.StartDatagramListenerAsync, cancellationToken);
    }

    /// <summary>
    /// Binds the TCP listener with <paramref name="startConnectionListener"/>, then the UDP
    /// listener on its port with <paramref name="startDatagramListener"/>.
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="startConnectionListener">Starts the TCP listener for a listen URL.</param>
    /// <param name="startDatagramListener">Starts the UDP listener for the listen URL's scheme and host on the TCP listener's bound port.</param>
    /// <param name="cancellationToken">Passed to both.</param>
    /// <returns>The pair, once both are bound.</returns>
    /// <exception cref="ListenerBindException">Either listener could not be bound; nothing stays bound.</exception>
    internal static async ValueTask<SharedPortListenerPair> StartAsync(
        ListenUrl listenUrl,
        Func<ListenUrl, CancellationToken, ValueTask<IConnectionListener>> startConnectionListener,
        Func<ListenUrl, CancellationToken, ValueTask<IDatagramListener>> startDatagramListener,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var connectionListener = await startConnectionListener(listenUrl, cancellationToken);
            var (datagramListener, failure) = await StartOnSharedPortAsync(
                listenUrl, connectionListener.ListenUrl, attempt, startDatagramListener, cancellationToken);

            if (datagramListener is not null)
            {
                return new SharedPortListenerPair(connectionListener, datagramListener);
            }

            await connectionListener.DisposeAsync();
            failure?.Throw();
        }
    }

    /// <summary>
    /// Disposes both listeners, the UDP one first.
    /// </summary>
    /// <returns>A task that completes once both are disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        await DatagramListener.DisposeAsync();
        await ConnectionListener.DisposeAsync();
    }

    // The UDP listener on the TCP listener's port; or no listener and no failure when that port
    // was taken and the pair may be tried again; or no listener and the failure to throw once the
    // TCP listener is released.
    private static async ValueTask<(IDatagramListener? Listener, ExceptionDispatchInfo? Failure)> StartOnSharedPortAsync(
        ListenUrl listenUrl,
        ListenUrl boundListenUrl,
        int attempt,
        Func<ListenUrl, CancellationToken, ValueTask<IDatagramListener>> startDatagramListener,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await startDatagramListener(SharedPortOf(boundListenUrl), cancellationToken), null);
        }
        catch (Exception exception)
        {
            return (null, MayRetry(listenUrl, exception, attempt) ? null : ExceptionDispatchInfo.Capture(exception));
        }
    }

    // The TCP listener's listen URL keeps the port asked for (0) and carries the one it got as
    // BoundPort; the UDP listener is asked for that port itself.
    private static ListenUrl SharedPortOf(ListenUrl boundListenUrl) =>
        new(boundListenUrl.Scheme, boundListenUrl.Host, boundListenUrl.BoundPort ?? boundListenUrl.Port);

    private static bool MayRetry(ListenUrl listenUrl, Exception exception, int attempt) =>
        listenUrl.Port == 0
        && exception is ListenerBindException { Failure: ListenerBindFailure.AddressInUse }
        && attempt < MaxBindAttempts;
}
