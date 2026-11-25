namespace Surl.MessageBus;

/// <summary>
/// Subscribing several filters in one step (<see cref="BusConnection.SubscribeTogether"/>).
/// </summary>
public sealed partial class MessageBus
{
    internal IReadOnlyList<BusSubscribeResult> SubscribeTogether(BusConnection connection, IReadOnlyList<BusSubscribeRequest> requests, Action<BusMessage> deliver)
    {
        // The lock is reentrant: each subscribe below takes it again, and it is not let go
        // between them, so no publish is accepted in between.
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(connection.IsClosed, connection);
            ThrowIfNotHeldBy(connection, requests);
            return
            [
                .. requests.Select(request => request.Held is { } held
                    ? ReadRetainedAgain(held)
                    : Subscribe(connection, request.Filter, deliver, group: null, includeRetained: true, noLocal: false)),
            ];
        }
    }

    // Called under the lock, before anything is subscribed. A subscription another connection
    // holds would read topics this connection's account was never granted.
    private void ThrowIfNotHeldBy(BusConnection connection, IReadOnlyList<BusSubscribeRequest> requests)
    {
        if (requests.Any(request => request.Held is { } held && !(held.Connection == connection && subscriptions.Contains(held))))
        {
            throw new ArgumentException("A held subscription is not a live subscription of this connection.", nameof(requests));
        }
    }

    // Called under the lock.
    private BusSubscribeResult ReadRetainedAgain(BusSubscription held) =>
        new(MessageBusOutcome.Succeeded, held, RetainedMatching(held.FilterLevels, timeProvider.GetUtcNow()));
}
