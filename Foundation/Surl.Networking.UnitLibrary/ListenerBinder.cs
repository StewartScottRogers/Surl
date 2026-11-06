using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Binds every address of one listen URL on one port, or none of them (ADR-0004, section 6).
/// It decides the port each address is asked for and releases what it bound when one fails;
/// the bind itself is the caller's, so this runs without a socket in the fast tests.
/// </summary>
internal static class ListenerBinder
{
    /// <summary>
    /// How many times port 0 is tried across every address before an address-in-use failure
    /// is reported: the ephemeral port the first address got can already be taken on another.
    /// </summary>
    public const int EphemeralPortAttempts = 5;

    /// <summary>
    /// Binds each of <paramref name="addresses"/> in order. The first is asked for
    /// <paramref name="listenUrl"/>'s port; every later one is asked for the port the first got,
    /// so port 0 ends with every address on the same ephemeral port. With port 0, a later
    /// address finding that port in use starts again from the first, up to
    /// <see cref="EphemeralPortAttempts"/> times.
    /// </summary>
    /// <typeparam name="TListener">What a bind produces; a listening <see cref="Socket"/> in production.</typeparam>
    /// <param name="listenUrl">The listen URL being bound, for its port and for the exception.</param>
    /// <param name="addresses">The addresses to bind; at least one.</param>
    /// <param name="bind">Binds one endpoint and starts listening on it.</param>
    /// <param name="boundPort">Reads the port a listener got.</param>
    /// <param name="release">Releases a listener bound before a later address failed.</param>
    /// <returns>One listener per address, in the order of <paramref name="addresses"/>.</returns>
    /// <exception cref="ListenerBindException">
    /// An address could not be bound; every listener bound before it has been released.
    /// </exception>
    public static IReadOnlyList<TListener> BindAll<TListener>(
        ListenUrl listenUrl,
        IReadOnlyList<IPAddress> addresses,
        Func<IPEndPoint, TListener> bind,
        Func<TListener, int> boundPort,
        Action<TListener> release)
    {
        if (addresses.Count == 0)
        {
            throw new ArgumentException("A listen URL binds at least one address.", nameof(addresses));
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return BindEach(listenUrl, addresses, bind, boundPort, release);
            }
            catch (ListenerBindException exception) when (MayTryAnotherEphemeralPort(listenUrl, exception, attempt))
            {
                // The ephemeral port the first address got was taken on a later one; start again.
            }
        }
    }

    /// <summary>
    /// Releases every listener in <paramref name="listeners"/>.
    /// </summary>
    /// <typeparam name="TListener">What a bind produced.</typeparam>
    /// <param name="listeners">The listeners to release.</param>
    /// <param name="release">Releases one listener.</param>
    public static void ReleaseAll<TListener>(IEnumerable<TListener> listeners, Action<TListener> release)
    {
        foreach (var listener in listeners)
        {
            release(listener);
        }
    }

    private static bool MayTryAnotherEphemeralPort(ListenUrl listenUrl, ListenerBindException exception, int attempt) =>
        listenUrl.Port == 0 && exception.Failure == ListenerBindFailure.AddressInUse && attempt < EphemeralPortAttempts;

    private static List<TListener> BindEach<TListener>(
        ListenUrl listenUrl,
        IReadOnlyList<IPAddress> addresses,
        Func<IPEndPoint, TListener> bind,
        Func<TListener, int> boundPort,
        Action<TListener> release)
    {
        var bound = new List<TListener>(addresses.Count);
        var port = listenUrl.Port;

        foreach (var address in addresses)
        {
            var endPoint = new IPEndPoint(address, port);
            port = BindOne(listenUrl, endPoint, bind, boundPort, release, bound);
        }

        return bound;
    }

    private static int BindOne<TListener>(
        ListenUrl listenUrl,
        IPEndPoint endPoint,
        Func<IPEndPoint, TListener> bind,
        Func<TListener, int> boundPort,
        Action<TListener> release,
        List<TListener> bound)
    {
        try
        {
            bound.Add(bind(endPoint));

            return boundPort(bound[^1]);
        }
        catch (Exception exception)
        {
            ReleaseAll(bound, release);

            if (exception is SocketException socketException)
            {
                throw new ListenerBindException(
                    listenUrl, endPoint, BindFailureClassifier.Classify(socketException.SocketErrorCode), socketException);
            }

            throw;
        }
    }
}
