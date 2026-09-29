using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// A started listener that accepts stream-oriented connections (ADR-0004, section 6).
/// Disposing it stops listening; connections already accepted are unaffected.
/// </summary>
public interface IConnectionListener : IAsyncDisposable
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
    /// Waits for the next connection.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The accepted connection.</returns>
    ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken);
}
