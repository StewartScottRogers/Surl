namespace Surl.Extended.Protocol.Amqp1;

/// <summary>A delivery the container sent unsettled, waiting for the client receiver's <c>disposition</c>.</summary>
/// <param name="Session">The session it was sent in.</param>
/// <param name="DeliveryId">Its delivery-id in that session.</param>
/// <param name="Link">The link it was sent on.</param>
/// <param name="QueueDeliveryTag">The bus consumer's delivery tag for a queue message; <see langword="null"/> for a topic's.</param>
internal sealed record Amqp1OutgoingDelivery(Amqp1Session Session, uint DeliveryId, Amqp1Link Link, long? QueueDeliveryTag)
{
    /// <summary>Whether the client's outcome has been applied to the bus.</summary>
    public bool OutcomeApplied { get; set; }

    /// <summary>The outcome a transaction holds for it; <see langword="null"/> when none does.</summary>
    public Amqp1HeldOutcome? Held { get; set; }

    /// <summary>Drops the outcome a transaction holds for it, as a later <c>disposition</c> replaces it.</summary>
    public void ForgetHeldOutcome()
    {
        if (Held is { } held)
        {
            held.Transaction.HeldOutcomes.Remove(held);
            Held = null;
        }
    }
}
