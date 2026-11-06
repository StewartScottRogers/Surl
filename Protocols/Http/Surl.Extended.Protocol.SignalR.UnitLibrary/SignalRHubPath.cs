namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Where a hub is served: its <c>[endpoint-base]</c>, the listen URL's path with any trailing
/// <c>/</c> removed, and its negotiate path (TransportProtocols.md "negotiate"; ADR-0119, decision 1).
/// </summary>
public sealed record SignalRHubPath
{
    private SignalRHubPath(string endpointBase, string negotiatePath)
    {
        EndpointBase = endpointBase;
        NegotiatePath = negotiatePath;
    }

    /// <summary>
    /// The path the WebSocket transport upgrades on: <c>/chat</c> for
    /// <c>signalr://0.0.0.0:5000/chat/</c>, and <c>/</c> for a listen URL with no path.
    /// </summary>
    public string EndpointBase { get; }

    /// <summary>
    /// <see cref="EndpointBase"/> followed by <c>/negotiate</c>; <c>/negotiate</c> for the root hub.
    /// </summary>
    public string NegotiatePath { get; }

    /// <summary>
    /// The hub path a listen URL's path names.
    /// </summary>
    /// <param name="listenPath">The listen URL's path, empty or starting with <c>/</c>.</param>
    /// <returns>The hub path.</returns>
    public static SignalRHubPath FromListenPath(string listenPath)
    {
        ArgumentNullException.ThrowIfNull(listenPath);

        var trimmed = listenPath.TrimEnd('/');

        return trimmed.Length == 0
            ? new SignalRHubPath("/", "/negotiate")
            : new SignalRHubPath(trimmed, trimmed + "/negotiate");
    }
}
