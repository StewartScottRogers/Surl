namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The broker's consumers by queue name, shared by all its connections (ADR-0112 decision 5,
/// <c>queue.delete</c>): the bus ends a deleted queue's consumers without telling their
/// connections, so the connection that deletes a queue asks the registry to end the rest.
/// </summary>
internal sealed class Amqp0ConsumerRegistry
{
    private readonly Lock gate = new();
    private readonly List<Amqp0ConsumerRegistration> registrations = [];

    /// <summary>Records that <paramref name="registration"/>'s consumer reads its queue.</summary>
    /// <param name="registration">The consumer's registration.</param>
    public void Add(Amqp0ConsumerRegistration registration)
    {
        lock (gate)
        {
            registrations.Add(registration);
        }
    }

    /// <summary>Forgets <paramref name="registration"/>; nothing when it is not recorded.</summary>
    /// <param name="registration">The consumer's registration.</param>
    public void Remove(Amqp0ConsumerRegistration registration)
    {
        lock (gate)
        {
            registrations.Remove(registration);
        }
    }

    /// <summary>Forgets every registration made by <paramref name="owner"/>.</summary>
    /// <param name="owner">The connection that ended.</param>
    public void RemoveOwnedBy(object owner)
    {
        lock (gate)
        {
            registrations.RemoveAll(registration => ReferenceEquals(registration.Owner, owner));
        }
    }

    /// <summary>Forgets and ends every consumer of <paramref name="queue"/>.</summary>
    /// <param name="queue">The queue that was deleted.</param>
    public void EndConsumersOf(string queue)
    {
        List<Amqp0ConsumerRegistration> ended;
        lock (gate)
        {
            ended = registrations.FindAll(registration => string.Equals(registration.Queue, queue, StringComparison.Ordinal));
            registrations.RemoveAll(ended.Contains);
        }

        // Outside the lock: each ending takes its channel's gate and queues a frame.
        ended.ForEach(registration => registration.End());
    }
}

/// <summary>One consumer's entry in the <see cref="Amqp0ConsumerRegistry"/>.</summary>
/// <param name="Queue">The queue it reads.</param>
/// <param name="Owner">The connection that made it.</param>
/// <param name="End">Ends the consumer on its connection after its queue was deleted.</param>
internal sealed record Amqp0ConsumerRegistration(string Queue, object Owner, Action End);
