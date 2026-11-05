namespace Surl.MessageBus;

/// <summary>
/// What <see cref="BusConnection.GetFromQueue"/> did (AMQP 0-9-1 <c>basic.get</c>).
/// </summary>
/// <param name="Outcome"><see cref="MessageBusOutcome.Succeeded"/> whether or not a message was
/// ready, or which refusal the get met.</param>
/// <param name="Delivery">The message taken, held unacknowledged for the connection until
/// <see cref="BusConnection.AcknowledgeGet"/> or <see cref="BusConnection.RejectGet"/> settles it or
/// the connection ends; <see langword="null"/> when none was ready (<c>get-empty</c>) or refused.</param>
/// <param name="ReadyMessageCount">How many messages are still ready after it (<c>get-ok</c>'s
/// <c>message-count</c>); zero when refused.</param>
public sealed record BusGetResult(MessageBusOutcome Outcome, BusQueueDelivery? Delivery, int ReadyMessageCount);
