namespace Surl.Extended.Protocol.Amqp1;

/// <summary>A delivery the container sent unsettled, waiting for the client receiver's <c>disposition</c>.</summary>
/// <param name="Link">The link it was sent on.</param>
/// <param name="QueueDeliveryTag">The bus consumer's delivery tag for a queue message; <see langword="null"/> for a topic's.</param>
internal sealed record Amqp1OutgoingDelivery(Amqp1Link Link, long? QueueDeliveryTag)
{
    /// <summary>Whether the client's outcome has been applied to the bus.</summary>
    public bool OutcomeApplied { get; set; }
}
