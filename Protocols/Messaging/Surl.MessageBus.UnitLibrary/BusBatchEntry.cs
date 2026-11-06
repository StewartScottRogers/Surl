namespace Surl.MessageBus;

/// <summary>
/// One message of a batch <see cref="BusConnection.PublishBatch"/> publishes all or none: either a
/// topic publish, as <see cref="BusConnection.Publish(BusMessage)"/> makes, or a send straight to a
/// named queue, as <see cref="MessageBus.PublishToQueue(string, BusMessage, string?)"/> makes.
/// </summary>
public sealed record BusBatchEntry
{
    private BusBatchEntry(string? queueName, BusMessage message)
    {
        QueueName = queueName;
        Message = message;
    }

    /// <summary>The named queue the message is sent straight to; <see langword="null"/> for a
    /// topic publish.</summary>
    public string? QueueName { get; }

    /// <summary>The message.</summary>
    public BusMessage Message { get; }

    /// <summary>An entry publishing <paramref name="message"/> on its topic.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static BusBatchEntry ToTopic(BusMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new(null, message);
    }

    /// <summary>An entry sending <paramref name="message"/> straight to the queue
    /// <paramref name="queueName"/>; its topic is not checked.</summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="message">The message.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static BusBatchEntry ToQueue(string queueName, BusMessage message)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        ArgumentNullException.ThrowIfNull(message);
        return new(queueName, message);
    }
}
