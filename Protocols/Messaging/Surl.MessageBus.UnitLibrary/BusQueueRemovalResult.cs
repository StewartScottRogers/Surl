namespace Surl.MessageBus;

/// <summary>
/// What <see cref="BusConnection.PurgeQueue"/> or a conditional
/// <see cref="BusConnection.DeleteQueue(string, bool, bool)"/> did.
/// </summary>
/// <param name="Outcome">Whether the messages were removed, or which refusal it met.</param>
/// <param name="RemovedMessageCount">How many messages it removed (AMQP 0-9-1 <c>purge-ok</c>'s and
/// <c>delete-ok</c>'s <c>message-count</c>); zero when refused.</param>
public sealed record BusQueueRemovalResult(MessageBusOutcome Outcome, int RemovedMessageCount);