namespace Surl.MessageBus;

/// <summary>
/// What <see cref="BusConnection.DeclareQueue"/> did.
/// </summary>
/// <param name="Outcome">Whether the queue now exists with the flags asked for, or which refusal
/// the declare met.</param>
/// <param name="ReadyMessageCount">How many of the queue's messages wait for a consumer,
/// unacknowledged ones not counted (AMQP 0-9-1 <c>queue.declare-ok</c>'s
/// <c>message-count</c>); zero when refused.</param>
/// <param name="ConsumerCount">How many consumers the queue has; zero when refused.</param>
public sealed record BusQueueDeclareResult(MessageBusOutcome Outcome, int ReadyMessageCount, int ConsumerCount);
