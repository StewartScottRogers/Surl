using Surl.Extended.Protocol.Amqp0;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the exchanges and bindings of the composed AMQP 0-9-1 brokers to whole-instance reset, snapshot and
/// restore (ADR-0159 decisions 2 and 3). In the order across stores they come after the accounts (and after the
/// proxy accounts, TLS-SRP accounts, Redis keyspace and LDAP directory when the instance holds them) and before
/// the Kafka producers.
/// </summary>
internal static class Amqp0ExchangeStoreRegistration
{
    /// <summary>
    /// Builds the entry that controls every broker's exchange table, publishing one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run.
    /// </summary>
    /// <param name="brokers">The composed AMQP 0-9-1 brokers.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <returns>The entry, or <see langword="null"/> when no broker is composed.</returns>
    public static ComposedStoreRegistration? For(IReadOnlyList<Amqp0Broker> brokers, ISurlEventPublisher events) =>
        brokers.Count == 0
            ? null
            : new ComposedStoreRegistration(
                StoreKind.Exchanges,
                _ =>
                {
                    foreach (var broker in brokers)
                    {
                        broker.ResetExchanges();
                    }

                    events.Publish(new StoreChangedEvent(StoreKind.Exchanges, StoreChange.Reset, string.Empty, null, null));
                    return Task.CompletedTask;
                },
                (_, _) =>
                {
                    var snapshots = brokers.Select(broker => broker.TakeExchangeSnapshot()).ToList();
                    return Task.FromResult(new TakenSnapshot(
                        StoreKind.Exchanges,
                        snapshots.Sum(snapshot => snapshot.ByteCount),
                        _ =>
                        {
                            for (var index = 0; index < brokers.Count; index++)
                            {
                                brokers[index].RestoreExchanges(snapshots[index]);
                            }

                            events.Publish(new StoreChangedEvent(StoreKind.Exchanges, StoreChange.Restored, string.Empty, null, null));
                            return Task.CompletedTask;
                        }));
                });
}
