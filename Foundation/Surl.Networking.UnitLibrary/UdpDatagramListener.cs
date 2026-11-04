using System.Diagnostics.CodeAnalysis;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Listens for UDP datagrams on every address a listen URL names, on one port, and hands the
/// first datagram from each new remote endpoint out as an <see cref="IDatagramFlow"/>
/// (ADR-0004, sections 3 and 6). A flow can move to a fresh ephemeral port, RFC 1350's new
/// transfer identifier, which TFTP needs.
/// </summary>
/// <remarks>
/// Only <see cref="StartAsync(ListenUrl, CancellationToken)"/> and <see cref="UdpDatagramSocket"/>
/// touch a socket; the decisions - which addresses, which port, which flow a datagram belongs
/// to, when the listen sockets close - live in <see cref="ListenAddressResolver"/>,
/// <see cref="ListenerBinder"/>, <see cref="DatagramDemultiplexer"/> and
/// <see cref="DemultiplexedDatagramFlow"/>, which the fast tests cover (ADR-0004, section 8).
/// Disposing the listener stops new flows; the listen sockets close once no flow handed out
/// still sends from them. <see cref="AcceptFlowAsync"/> is meant for one caller at a time.
/// </remarks>
public sealed class UdpDatagramListener : IDatagramListener
{
    private readonly DatagramDemultiplexer demultiplexer;

    private UdpDatagramListener(
        ListenUrl listenUrl, IReadOnlyList<IDatagramSocket> listenSockets, Func<IPEndPoint, IDatagramSocket> bindSocket)
    {
        BoundEndPoints = [.. listenSockets.Select(socket => socket.LocalEndPoint)];
        ListenUrl = listenUrl.WithBoundPort(BoundPortOf(listenSockets[0]));
        demultiplexer = new DatagramDemultiplexer(listenSockets, bindSocket);
    }

    /// <inheritdoc/>
    public ListenUrl ListenUrl { get; }

    /// <inheritdoc/>
    public IReadOnlyList<EndPoint> BoundEndPoints { get; }

    /// <summary>
    /// Binds every address <paramref name="listenUrl"/> names and starts listening for UDP
    /// datagrams. An IP literal binds that address; a host name is resolved with
    /// <see cref="Dns"/> and every address it resolves to is bound, on the port the first got
    /// when <see cref="ListenUrl.Port"/> is 0.
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="cancellationToken">Cuts the host-name resolution off.</param>
    /// <returns>The listener, once every address is bound; its listen URL carries the bound port.</returns>
    /// <exception cref="ListenerBindException">
    /// The host did not resolve, or an address could not be bound; nothing stays bound.
    /// </exception>
    // Passes Dns.GetHostAddressesAsync and UdpDatagramSocket.Bind to the tested overload.
    [ExcludeFromCodeCoverage(Justification = "Resolves with Dns and binds sockets; covered by the integration tests.")]
    public static ValueTask<UdpDatagramListener> StartAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        StartAsync(listenUrl, Dns.GetHostAddressesAsync, UdpDatagramSocket.Bind, cancellationToken);

    /// <summary>
    /// Binds every address <paramref name="listenUrl"/> names with <paramref name="bindSocket"/>
    /// and starts listening on them.
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="resolveHostName">Resolves a host name; <c>Dns.GetHostAddressesAsync</c> in production.</param>
    /// <param name="bindSocket">Binds a UDP socket to an endpoint, for the listen sockets and for every flow that moves.</param>
    /// <param name="cancellationToken">Cuts the host-name resolution off.</param>
    /// <returns>The listener, once every address is bound.</returns>
    /// <exception cref="ListenerBindException">
    /// The host did not resolve, or an address could not be bound; nothing stays bound.
    /// </exception>
    internal static async ValueTask<UdpDatagramListener> StartAsync(
        ListenUrl listenUrl,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveHostName,
        Func<IPEndPoint, IDatagramSocket> bindSocket,
        CancellationToken cancellationToken)
    {
        var addresses = await new ListenAddressResolver(resolveHostName).ResolveAsync(listenUrl, cancellationToken);
        var sockets = ListenerBinder.BindAll(listenUrl, addresses, bindSocket, BoundPortOf, socket => socket.Dispose());

        return new UdpDatagramListener(listenUrl, sockets, bindSocket);
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The listener was disposed.</exception>
    /// <exception cref="IOException">A listen socket failed.</exception>
    public ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken) =>
        demultiplexer.AcceptFlowAsync(cancellationToken);

    /// <summary>
    /// Stops opening flows and disposes those opened but not yet handed out. Flows already
    /// handed out are unaffected. Calling it twice is harmless.
    /// </summary>
    /// <returns>A task that completes once flows have stopped opening.</returns>
    public ValueTask DisposeAsync() => demultiplexer.StopAsync();

    private static int BoundPortOf(IDatagramSocket socket) => ((IPEndPoint)socket.LocalEndPoint).Port;
}
