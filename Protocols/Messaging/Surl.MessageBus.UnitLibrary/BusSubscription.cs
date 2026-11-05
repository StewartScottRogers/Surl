namespace Surl.MessageBus;

/// <summary>
/// One live subscription on a <see cref="MessageBus"/>, made by
/// <see cref="BusConnection.Subscribe"/>. Every message published after it began on a topic its
/// <see cref="Filter"/> matches is handed to its delivery callback, in publish order, until it
/// is disposed or its connection is.
/// </summary>
public sealed class BusSubscription : IDisposable
{
    private readonly MessageBus bus;
    private readonly Action<BusMessage> deliver;
    private volatile bool active = true;

    internal BusSubscription(MessageBus bus, BusConnection connection, string filter, string? group, Action<BusMessage> deliver)
    {
        this.bus = bus;
        this.deliver = deliver;
        Connection = connection;
        Filter = filter;
        FilterLevels = CanonicalTopicName.SplitLevels(filter);
        Group = group;
    }

    /// <summary>
    /// The canonical filter it was made with.
    /// </summary>
    public string Filter { get; }

    /// <summary>
    /// Its fan-out group (an MQTT 5 shared subscription's or a NATS queue group's name), or
    /// <see langword="null"/>. Among a group's subscriptions with the same filter, each message
    /// goes to one, round robin (ADR-0100, decision 3.3).
    /// </summary>
    public string? Group { get; }

    internal BusConnection Connection { get; }

    internal string[] FilterLevels { get; }

    /// <summary>
    /// Ends the subscription: no message is handed to it afterwards. Disposing it again does nothing.
    /// </summary>
    public void Dispose() => bus.Unsubscribe(this);

    internal void Deactivate() => active = false;

    internal void Deliver(BusMessage message)
    {
        if (active)
        {
            deliver(message);
        }
    }
}
