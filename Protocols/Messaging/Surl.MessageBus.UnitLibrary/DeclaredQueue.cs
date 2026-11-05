namespace Surl.MessageBus;

/// <summary>
/// One <c>--queue &lt;name&gt;[;durable]</c> (ADR-0102, decision 3.2): a named queue that exists
/// from start, is never exclusive or auto-deleted, and that no client may delete. A durable one
/// is persisted with its messages when the bus is loaded from a data directory; any other is
/// made empty at every start.
/// </summary>
public sealed record DeclaredQueue
{
    /// <summary>
    /// Declares a named queue.
    /// </summary>
    /// <param name="name">The queue's name; not empty.</param>
    /// <param name="durable">Whether it and its messages are persisted.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public DeclaredQueue(string name, bool durable = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Durable = durable;
    }

    /// <summary>The queue's name.</summary>
    public string Name { get; }

    /// <summary>Whether it and its messages are persisted.</summary>
    public bool Durable { get; }

    /// <summary>The flags the queue is made with.</summary>
    internal BusQueueFlags Flags => new(Durable: Durable);
}
