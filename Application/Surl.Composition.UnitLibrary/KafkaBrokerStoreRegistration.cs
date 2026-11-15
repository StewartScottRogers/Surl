using Surl.Extended.Protocol.Kafka;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the producer states and consumer groups of the composed Kafka brokers to whole-instance reset, snapshot and
/// restore (ADR-0159 decisions 2 and 3), after the bus (decision 3).
/// </summary>
internal static class KafkaBrokerStoreRegistration
{
    /// <summary>
    /// Builds the entry that controls every broker's state, publishing one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run.
    /// </summary>
    /// <param name="brokers">The composed Kafka brokers.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <returns>The entry, or <see langword="null"/> when no broker is composed.</returns>
    public static ComposedStoreRegistration? For(IReadOnlyList<KafkaBroker> brokers, ISurlEventPublisher events) =>
        brokers.Count == 0
            ? null
            : new ComposedStoreRegistration(
                StoreKind.Producers,
                _ =>
                {
                    foreach (var broker in brokers)
                    {
                        broker.State.Reset();
                    }

                    events.Publish(new StoreChangedEvent(StoreKind.Producers, StoreChange.Reset, string.Empty, null, null));
                    return Task.CompletedTask;
                },
                (_, _) =>
                {
                    var snapshots = brokers.Select(broker => broker.State.TakeSnapshot()).ToList();
                    return Task.FromResult(new TakenSnapshot(
                        StoreKind.Producers,
                        snapshots.Sum(snapshot => snapshot.ByteCount),
                        _ =>
                        {
                            for (var index = 0; index < brokers.Count; index++)
                            {
                                brokers[index].State.Restore(snapshots[index]);
                            }

                            events.Publish(new StoreChangedEvent(StoreKind.Producers, StoreChange.Restored, string.Empty, null, null));
                            return Task.CompletedTask;
                        }));
                });
}
