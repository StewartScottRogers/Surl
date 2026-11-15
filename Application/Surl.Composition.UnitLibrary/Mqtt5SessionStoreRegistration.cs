using Surl.Extended.Protocol.Mqtt5;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the sessions and pending Wills of the composed MQTT 5 brokers to whole-instance reset, snapshot and
/// restore (ADR-0159 decisions 2 and 3), after the bus (decision 3).
/// </summary>
internal static class Mqtt5SessionStoreRegistration
{
    /// <summary>
    /// Builds the entry that controls every broker's sessions, publishing one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run.
    /// </summary>
    /// <param name="brokers">The composed MQTT 5 brokers.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <returns>The entry, or <see langword="null"/> when no broker is composed.</returns>
    public static ComposedStoreRegistration? For(IReadOnlyList<Mqtt5Broker> brokers, ISurlEventPublisher events) =>
        brokers.Count == 0
            ? null
            : new ComposedStoreRegistration(
                StoreKind.Sessions,
                _ =>
                {
                    foreach (var broker in brokers)
                    {
                        broker.ResetSessions();
                    }

                    events.Publish(new StoreChangedEvent(StoreKind.Sessions, StoreChange.Reset, string.Empty, null, null));
                    return Task.CompletedTask;
                },
                (_, _) =>
                {
                    var snapshots = brokers.Select(broker => broker.TakeSessionSnapshot()).ToList();
                    return Task.FromResult(new TakenSnapshot(
                        StoreKind.Sessions,
                        snapshots.Sum(snapshot => snapshot.ByteCount),
                        _ =>
                        {
                            for (var index = 0; index < brokers.Count; index++)
                            {
                                brokers[index].RestoreSessions(snapshots[index]);
                            }

                            events.Publish(new StoreChangedEvent(StoreKind.Sessions, StoreChange.Restored, string.Empty, null, null));
                            return Task.CompletedTask;
                        }));
                });
}
