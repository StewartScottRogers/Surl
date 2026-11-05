namespace Surl.MessageBus;

/// <summary>
/// What <see cref="BusConnection.InspectQueue"/> found, without creating the queue (AMQP 0-9-1
/// <c>queue.declare</c> with <c>passive</c>).
/// </summary>
/// <param name="Outcome"><see cref="MessageBusOutcome.Succeeded"/> when the queue exists and this
/// connection may use it, or which refusal the inspection met.</param>
/// <param name="ReadyMessageCount">How many of its messages wait for a consumer, unacknowledged
/// ones not counted; zero when refused.</param>
/// <param name="ConsumerCount">How many consumers still take deliveries from it; zero when refused.</param>
public sealed record BusQueueInspectResult(MessageBusOutcome Outcome, int ReadyMessageCount, int ConsumerCount);