using System.Collections.Concurrent;
using Surl.Extended.Protocol.SignalR;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Serves each <c>signalr</c> and <c>signalrs</c> listen URL's hub at that listen URL's own path, with
/// its own connections, so two listen URLs of one run serve two hubs (ADR-0153; ADR-0119 decisions 0 and 1).
/// </summary>
/// <remarks>
/// A <see cref="SignalRProtocolServer"/> is built on a path's first exchange and kept for the run; paths are
/// compared as the hub path, so <c>/chat</c> and <c>/chat/</c> are one hub.
/// </remarks>
public sealed class SignalRHubRoutingProtocolServer : IConnectionProtocolServer
{
    private readonly Func<SignalRHubPath, SignalRProtocolServer> createServer;
    private readonly ConcurrentDictionary<string, SignalRProtocolServer> servers = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the router.
    /// </summary>
    /// <param name="createServer">Builds the server that serves one hub path, with its own connections.</param>
    public SignalRHubRoutingProtocolServer(Func<SignalRHubPath, SignalRProtocolServer> createServer)
    {
        ArgumentNullException.ThrowIfNull(createServer);

        this.createServer = createServer;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [SignalRProtocolServer.PlainScheme, SignalRProtocolServer.TlsScheme];

    /// <summary>
    /// Gets the hub path a listen URL's path names: the root for a listen URL with no path.
    /// </summary>
    /// <param name="listenUrl">The listen URL.</param>
    /// <returns>The hub path.</returns>
    public static SignalRHubPath HubPathFor(ListenUrl listenUrl)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);

        return SignalRHubPath.FromListenPath(listenUrl.Path ?? string.Empty);
    }

    /// <summary>
    /// Gets the server that serves a listen URL's hub, building it on first use.
    /// </summary>
    /// <param name="listenUrl">The listen URL.</param>
    /// <returns>The one server for that hub path.</returns>
    public SignalRProtocolServer ServerFor(ListenUrl listenUrl)
    {
        var hubPath = HubPathFor(listenUrl);

        return servers.GetOrAdd(hubPath.EndpointBase, _ => createServer(hubPath));
    }

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ServerFor(context.ListenUrl).ServeAsync(connection, context);
    }
}
