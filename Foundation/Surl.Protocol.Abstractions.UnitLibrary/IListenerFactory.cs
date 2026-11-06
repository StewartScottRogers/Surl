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

    /// <summary>
    /// Binds a connection listener for <paramref name="listenUrl"/>, then a datagram listener on
    /// the same addresses and the port it got (ADR-0082, decisions 5 and 6). By default each is
    /// started once with the methods above, and the connection listener is disposed when the
    /// datagram listener cannot bind; <c>Surl.Networking</c> also retries a port-0 pair whose UDP
    /// port is taken.
    /// </summary>
    /// <param name="listenUrl">What to listen on.</param>
    /// <param name="cancellationToken">Cuts the start off.</param>
    /// <returns>Both listeners, once both are bound.</returns>
    /// <exception cref="ListenerBindException">Either could not be bound, or the host did not resolve; nothing stays bound.</exception>
    async ValueTask<SharedPortListeners> StartSharedPortListenersAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);
        var connectionListener = await StartConnectionListenerAsync(listenUrl, cancellationToken);
        var bound = connectionListener.ListenUrl;
        Exception failure;
        try
        {
            var datagramListener = await StartDatagramListenerAsync(
                new ListenUrl(bound.Scheme, bound.Host, bound.BoundPort ?? bound.Port), cancellationToken);
            return new SharedPortListeners(connectionListener, datagramListener);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        // Released outside the catch, then the failure is thrown again as it was.
        await connectionListener.DisposeAsync();
        return await Task.FromException<SharedPortListeners>(failure);
    }
}
