using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// A started listener that opens a datagram flow for each new remote endpoint (ADR-0004,
/// section 6). Disposing it stops listening; flows already handed out are unaffected.
/// </summary>
public interface IDatagramListener : IAsyncDisposable
{
    /// <summary>
    /// The listen URL, with <see cref="ListenUrl.BoundPort"/> set.
    /// </summary>
    ListenUrl ListenUrl { get; }

    /// <summary>
    /// Every endpoint the listener bound.
    /// </summary>
    IReadOnlyList<EndPoint> BoundEndPoints { get; }

    /// <summary>
    /// Waits for the first datagram from a remote endpoint that has no open flow.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The flow that datagram opened.</returns>
    ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken);
}
