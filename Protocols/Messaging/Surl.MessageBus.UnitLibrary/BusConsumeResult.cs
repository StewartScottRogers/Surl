namespace Surl.MessageBus;

/// <summary>
/// What <see cref="BusConnection.Consume"/> did.
/// </summary>
/// <param name="Outcome">Whether the consumer was made, or which refusal it met.</param>
/// <param name="Consumer">The consumer, not yet started; <see langword="null"/> when refused.
/// Call <see cref="BusQueueConsumer.Start"/> once the server has told its client the consumer
/// exists.</param>
public sealed record BusConsumeResult(MessageBusOutcome Outcome, BusQueueConsumer? Consumer);
