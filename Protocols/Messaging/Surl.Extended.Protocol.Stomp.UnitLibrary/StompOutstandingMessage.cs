namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// A MESSAGE written under <c>client</c> or <c>client-individual</c> and not yet settled by ACK or
/// NACK (ADR-0118 decision 5).
/// </summary>
/// <param name="MessageId">The MESSAGE's <c>message-id</c>, unique on the connection.</param>
/// <param name="Subscription">The subscription it was delivered to.</param>
/// <param name="DeliveryTag">The bus queue's delivery tag; <see langword="null"/> for a topic message, which the bus is never told about.</param>
internal sealed record StompOutstandingMessage(long MessageId, StompSubscription Subscription, long? DeliveryTag);
