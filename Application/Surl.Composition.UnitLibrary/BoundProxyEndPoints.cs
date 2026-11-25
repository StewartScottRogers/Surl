using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The end points one start's proxy and SOCKS listeners have bound, kept so the allowed-origin rule
/// can refuse a request for the proxy itself when it listens on port 0 and so had no port to refuse
/// when the rule was built (ADR-0088 decision 2). It belongs to one start; nothing is shared, and it
/// is safe to read while listeners bind.
/// </summary>
internal sealed class BoundProxyEndPoints
{
    private readonly object gate = new();
    private IReadOnlyList<IPEndPoint> endPoints = [];

    /// <summary>The end points bound so far, in the order they bound.</summary>
    internal IReadOnlyList<IPEndPoint> Snapshot()
    {
        lock (gate)
        {
            return endPoints;
        }
    }

    /// <summary>
    /// Keeps <paramref name="boundListenUrl"/>'s end point when it is a proxy or SOCKS listener on
    /// a port the system chose; any other listen URL is ignored. A host that is not an address
    /// refuses its port on every address.
    /// </summary>
    /// <param name="boundListenUrl">A listen URL as its listener reports it once bound.</param>
    internal void Record(ListenUrl boundListenUrl)
    {
        if (!ProxyComposition.IsProxyListenUrl(boundListenUrl) || boundListenUrl.BoundPort is not { } port)
        {
            return;
        }

        var endPoint = new IPEndPoint(ProxyComposition.AddressOf(boundListenUrl), port);
        lock (gate)
        {
            endPoints = [.. endPoints, endPoint];
        }
    }
}
