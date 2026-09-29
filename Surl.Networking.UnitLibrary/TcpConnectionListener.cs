using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Listens for TCP connections on every address a listen URL names, on one port, and hands
/// each accepted connection out as an <see cref="IConnection"/> (ADR-0004, section 6).
/// </summary>
/// <remarks>
/// Every member here calls a socket and is exercised by the integration tests; the decisions
/// behind them - which addresses, which port, what a failure means, which accept wins - live
/// in <see cref="ListenAddressResolver"/>, <see cref="ListenerBinder"/>,
/// <see cref="AcceptRace{TAccepted}"/> and <see cref="StreamConnection"/>, which the fast tests
/// cover (ADR-0004, section 8). <see cref="AcceptAsync"/> is meant for one caller at a time.
/// </remarks>
public sealed class TcpConnectionListener : IConnectionListener
{
    private readonly IReadOnlyList<Socket> listeningSockets;
    private readonly AcceptRace<Socket> acceptRace;
    private readonly ServerTlsHandshake? tlsHandshake;

    // Reads each listening socket's local endpoint and sets the bound port on the listen URL.
    [ExcludeFromCodeCoverage(Justification = "Reads Socket.LocalEndPoint; covered by the integration tests.")]
    private TcpConnectionListener(ListenUrl listenUrl, IReadOnlyList<Socket> listeningSockets, ServerTlsSettings? tlsSettings)
    {
        this.listeningSockets = listeningSockets;
        tlsHandshake = ServerTlsHandshake.ForListener(tlsSettings, listenUrl);
        BoundEndPoints = [.. listeningSockets.Select(LocalEndPointOf)];
        ListenUrl = listenUrl.WithBoundPort(BoundPortOf(listeningSockets[0]));
        acceptRace = new AcceptRace<Socket>(listeningSockets.Count, AcceptFromSocketAsync, ReleaseSocket);
    }

    /// <inheritdoc/>
    public ListenUrl ListenUrl
    {
        // Returns the value the socket-reading constructor set.
        [ExcludeFromCodeCoverage(Justification = "Set only by the socket-reading constructor; covered by the integration tests.")]
        get;
    }

    /// <inheritdoc/>
    public IReadOnlyList<EndPoint> BoundEndPoints
    {
        // Returns the value the socket-reading constructor set.
        [ExcludeFromCodeCoverage(Justification = "Set only by the socket-reading constructor; covered by the integration tests.")]
        get;
    }

    /// <summary>
    /// Binds every address <paramref name="listenUrl"/> names and starts listening for TCP
    /// connections. An IP literal binds that address; a host name is resolved with
    /// <see cref="Dns"/> and every address it resolves to is bound, on the port the first got
    /// when <see cref="ListenUrl.Port"/> is 0.
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="cancellationToken">Cuts the host-name resolution off.</param>
    /// <returns>The listener, once every address is bound; its listen URL carries the bound port.</returns>
    /// <exception cref="ListenerBindException">
    /// The host did not resolve, or an address could not be bound; nothing stays bound.
    /// </exception>
    // Resolves with Dns.GetHostAddressesAsync and binds with Socket.Bind and Socket.Listen.
    [ExcludeFromCodeCoverage(Justification = "Calls Dns and binds sockets; covered by the integration tests.")]
    public static ValueTask<TcpConnectionListener> StartAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        StartAsync(listenUrl, null, cancellationToken);

    /// <summary>
    /// Binds every address <paramref name="listenUrl"/> names, as
    /// <see cref="StartAsync(ListenUrl, CancellationToken)"/> does, and gives every accepted
    /// connection the TLS settings its <see cref="IConnection.UpgradeToTlsAsync"/> uses
    /// (ADR-0010), with the ALPN protocol IDs the listen URL's scheme offers.
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="tlsSettings">The process's TLS settings, or <see langword="null"/> for connections that cannot be secured.</param>
    /// <param name="cancellationToken">Cuts the host-name resolution off.</param>
    /// <returns>The listener, once every address is bound; its listen URL carries the bound port.</returns>
    /// <exception cref="ListenerBindException">
    /// The host did not resolve, or an address could not be bound; nothing stays bound.
    /// </exception>
    // Resolves with Dns.GetHostAddressesAsync and binds with Socket.Bind and Socket.Listen.
    [ExcludeFromCodeCoverage(Justification = "Calls Dns and binds sockets; covered by the integration tests.")]
    public static async ValueTask<TcpConnectionListener> StartAsync(
        ListenUrl listenUrl, ServerTlsSettings? tlsSettings, CancellationToken cancellationToken)
    {
        var addresses = await new ListenAddressResolver(Dns.GetHostAddressesAsync).ResolveAsync(listenUrl, cancellationToken);
        var sockets = ListenerBinder.BindAll(listenUrl, addresses, BindListeningSocket, BoundPortOf, ReleaseSocket);

        return new TcpConnectionListener(listenUrl, sockets, tlsSettings);
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The listener was disposed.</exception>
    // Wraps the accepted Socket in a NetworkStream-backed connection.
    [ExcludeFromCodeCoverage(Justification = "Accepts a socket; covered by the integration tests.")]
    public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken) =>
        CreateConnection(await acceptRace.AcceptNextAsync(cancellationToken));

    /// <summary>
    /// Stops listening and closes every listening socket. Connections already accepted are
    /// unaffected. Calling it twice is harmless.
    /// </summary>
    /// <returns>A task that completes once every listening socket is closed.</returns>
    // Disposes the listening sockets after stopping the accepts on them.
    [ExcludeFromCodeCoverage(Justification = "Disposes sockets; covered by the integration tests.")]
    public async ValueTask DisposeAsync()
    {
        var stopping = acceptRace.StopAsync();
        ListenerBinder.ReleaseAll(listeningSockets, ReleaseSocket);
        await stopping;
    }

    // Creates a TCP socket, then calls Socket.Bind and Socket.Listen on it. An IPv6 socket is
    // made IPv6-only first, so [::] binds only IPv6 on every platform, as Windows does by
    // default and Linux and macOS do not; the IPv4 and IPv6 integration tests cover both arms.
    [ExcludeFromCodeCoverage(Justification = "Binds a socket; covered by the integration tests.")]
    private static Socket BindListeningSocket(IPEndPoint endPoint)
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                socket.DualMode = false;
            }

            socket.Bind(endPoint);
            socket.Listen();

            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    // Reads Socket.LocalEndPoint.
    [ExcludeFromCodeCoverage(Justification = "Reads Socket.LocalEndPoint; covered by the integration tests.")]
    private static EndPoint LocalEndPointOf(Socket socket) => socket.LocalEndPoint!;

    // Reads the port of Socket.LocalEndPoint.
    [ExcludeFromCodeCoverage(Justification = "Reads Socket.LocalEndPoint; covered by the integration tests.")]
    private static int BoundPortOf(Socket socket) => ((IPEndPoint)socket.LocalEndPoint!).Port;

    // Calls Socket.Dispose.
    [ExcludeFromCodeCoverage(Justification = "Disposes a socket; covered by the integration tests.")]
    private static void ReleaseSocket(Socket socket) => socket.Dispose();

    // Wraps the accepted socket in a NetworkStream that owns it, with Nagle's algorithm off so
    // small replies are not held back waiting for the client's acknowledgement. A client that
    // reset before this runs makes the socket calls fail; the socket is released and the
    // failure is an IOException, like every other transport failure (ADR-0004, section 2).
    [ExcludeFromCodeCoverage(Justification = "Wraps an accepted socket; covered by the integration tests.")]
    private StreamConnection CreateConnection(Socket socket)
    {
        try
        {
            socket.NoDelay = true;

            return new StreamConnection(
                new NetworkStream(socket, ownsSocket: true),
                socket.LocalEndPoint!,
                socket.RemoteEndPoint!,
                new SocketTransportControl(socket),
                tlsHandshake);
        }
        catch (SocketException exception)
        {
            socket.Dispose();
            throw new IOException(exception.Message, exception);
        }
    }

    // Calls Socket.AcceptAsync on the listening socket with the given index.
    [ExcludeFromCodeCoverage(Justification = "Accepts on a socket; covered by the integration tests.")]
    private Task<Socket> AcceptFromSocketAsync(int index, CancellationToken cancellationToken) =>
        listeningSockets[index].AcceptAsync(cancellationToken).AsTask();
}
