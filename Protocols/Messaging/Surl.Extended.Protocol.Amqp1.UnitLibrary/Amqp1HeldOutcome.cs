namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A client receiver's outcome for a delivery the container sent, stated in a <c>transactional-state</c>
/// and so held in the transaction until a commit applies it (Part 4 section 4.4.2; ADR-0146 decision 1.5).
/// </summary>
/// <param name="Transaction">The transaction holding it.</param>
/// <param name="Delivery">The delivery it retires.</param>
/// <param name="Outcome">The outcome the <c>transactional-state</c> carried, as read.</param>
/// <param name="Settled">Whether the client's <c>disposition</c> also settled the delivery.</param>
internal sealed record Amqp1HeldOutcome(Amqp1Transaction Transaction, Amqp1OutgoingDelivery Delivery, object? Outcome, bool Settled);
