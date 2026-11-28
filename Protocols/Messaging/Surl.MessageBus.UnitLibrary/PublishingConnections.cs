namespace Surl.MessageBus;

/// <summary>
/// The client connections being served now that may publish on one <see cref="MessageBus"/>
/// (<see cref="MessageBus.PublishingConnections"/>), oldest first, whatever protocol each speaks.
/// </summary>
public sealed class PublishingConnections
{
    private readonly Lock gate = new();
    private readonly List<PublishingConnectionProgress> oldestFirst = [];

    internal PublishingConnections()
    {
    }

    /// <summary>
    /// The longest <see cref="PublishingConnectionProgress.WaitForEarlierConnectionsAsync"/> waits.
    /// </summary>
    public static TimeSpan MaxWaitForEarlierConnections { get; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Joins a connection as the newest.
    /// </summary>
    /// <returns>The connection's progress, which it disposes when it ends.</returns>
    public PublishingConnectionProgress Join()
    {
        var connection = new PublishingConnectionProgress(this);
        lock (gate)
        {
            oldestFirst.Add(connection);
        }

        return connection;
    }

    internal void Remove(PublishingConnectionProgress connection)
    {
        lock (gate)
        {
            oldestFirst.Remove(connection);
        }
    }

    // Completes once every connection that joined before this one, and has not left, has caught up
    // at least once. A connection that has left has no earlier ones.
    internal Task EarlierThanHaveCaughtUp(PublishingConnectionProgress connection)
    {
        lock (gate)
        {
            var position = Math.Max(oldestFirst.IndexOf(connection), 0);
            return Task.WhenAll(oldestFirst.Take(position).Select(earlier => earlier.CaughtUp));
        }
    }
}
