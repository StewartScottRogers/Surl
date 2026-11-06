namespace Surl.MessageBus;

/// <summary>
/// What <see cref="BusConnection.PublishBatch"/> did with a batch: every entry published, or none.
/// </summary>
/// <param name="Outcome"><see cref="MessageBusOutcome.Succeeded"/> when every entry was published,
/// otherwise why the first refused entry was refused - the outcome its own publish would have
/// met.</param>
/// <param name="RefusedIndex">The index in the batch of the first entry refused;
/// <see langword="null"/> when the batch was published.</param>
/// <param name="Results">One result per entry, in batch order, when the batch was published; empty
/// when it was refused. A topic entry's is what <see cref="BusConnection.Publish(BusMessage)"/>
/// would have answered; a queue entry's has <see cref="BusPublishResult.QueuedCount"/> 1, or 0 when
/// the message had already expired and was dropped.</param>
public sealed record BusBatchPublishResult(MessageBusOutcome Outcome, int? RefusedIndex, IReadOnlyList<BusPublishResult> Results);
