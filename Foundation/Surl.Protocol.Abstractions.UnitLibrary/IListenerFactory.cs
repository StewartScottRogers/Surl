namespace Surl.Protocol.Abstractions;

/// <summary>
/// Starts the listeners for listen URLs (ADR-0004, section 6). <c>Surl.Networking</c>
/// implements it.
/// </summary>
public interface IListenerFactory
{
    /// <summary>
    /// Binds every address <paramref name="listenUrl"/> names and listens for connections.
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="cancellationToken">Cuts the start off.</param>
    /// <returns>The listener, once every address is bound; its listen URL carries the bound port.</returns>
    /// <exception cref="ListenerBindException">An address could not be bound, or the host did not resolve.</exception>
    ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Binds every address <paramref name="listenUrl"/> names and listens for datagrams.
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="cancellationToken">Cuts the start off.</param>
    /// <returns>The listener, once every address is bound; its listen URL carries the bound port.</returns>
    /// <exception cref="ListenerBindException">An address could not be bound, or the host did not resolve.</exception>
    ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken);
}
