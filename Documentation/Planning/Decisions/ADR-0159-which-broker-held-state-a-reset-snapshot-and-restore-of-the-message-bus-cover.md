# ADR-0159 — Which broker-held state a reset, snapshot and restore of the message bus cover

- **Status:** Accepted
- **Date:** 2026-11-14
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-14, in BL-1152.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) (decision 3: seed, reset, snapshot and
  restore; its "live sessions" and "stores on disk" rules), [ADR-0158](ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md)
  (decision 2's registration seam, `IControllableStore`, BL-1158), ADR-0100 (the shared stores), ADR-0093
  (MQTT 5), ADR-0105 (Kafka), ADR-0107 and ADR-0146 (AMQP 1.0) and ADR-0003.
- **Amends:** ADR-0124 decision 3 (a whole-instance reset, snapshot and restore also cover the state below,
  and the order across stores grows by it).
- **Acted on by:** BL-1170 (AMQP 0-9-1), BL-1171 (MQTT 5) and BL-1190 (Kafka).
- **Amended by:** [ADR-0166](ADR-0166-which-client-changes-to-the-amqp-0-9-1-exchange-table-the-mqtt-5-sessions-and-the-kafka-producer-states-publish-store-changed-events.md) (which client changes publish).
- **Amended (2026-11-27, BL-1857):** decisions 3 and 4's order across stores: the broker registrations follow the
  accounts, and content comes before the bus; see Amendment 1.
- **Amended (2026-11-27, BL-1896):** the registration seam decision 3 names (`IControllableStore`) is superseded by
  the one the code has; see Amendment 2, decided by Claude under Stewart's delegation.

## Context

ADR-0124 decision 3 makes `MessageBus.Reset` keep configured topics, queues, maps and access rules, holding no
messages, no retained messages and empty logs. Several brokers keep state of their own beside the bus, which
`ComposedStores.ResetAsync`, `TakeSnapshotAsync` and `RestoreAsync` never touch. After a reset that state
disagrees with the emptied bus: a persistent MQTT 5 session still holds messages the bus no longer has, a Kafka
producer's sequence window still remembers batches from a log that was emptied, and an AMQP 0-9-1 exchange one
test declared is still there for the next. Each broker's code was read for this audit.

## Decision

### 1. The audit

Classes: **configured** (what start-up builds, so reset returns to it), **test data** (what a test or client
created, so reset removes it) and **live session state** (belongs to connections or their resumable sessions;
ADR-0124's "live sessions" rule applies).

| Broker | State outside `MessageBus` | Class |
| --- | --- | --- |
| AMQP 0-9-1 | `Amqp0ExchangeTable`: built-in exchanges (default, `amq.direct`, `amq.fanout`, `amq.topic`, `amq.match`) and files loaded at start with `--directory` | configured |
| AMQP 0-9-1 | exchanges and bindings a client declared, with `Amqp0ExchangeFiles` persistence under `--directory` | test data |
| MQTT 5 | `Mqtt5BrokerState.sessions`: each `Mqtt5Session`'s subscriptions, queued deliveries, unacknowledged `PUBLISH`es and QoS 2 identifiers | live session state (resumable) |
| MQTT 5 | `pendingWills` (a Will waiting out its delay) | live session state |
| MQTT 5 | `connectedClients` (the registration of each open connection) | live session state |
| MQTT 3.1.1 | none: `MqttProtocolServer` holds only the bus and the policy; retained messages are `MqttBusRetainedMessages` on the bus | - |
| AMQP 1.0 | none: nodes are bus topics and queues; a durable or shared subscription is a bus named queue (`Amqp1SubscriptionQueue`, ADR-0146 decision 3 and Amendment 4), so it follows the bus | - |
| Kafka | `KafkaGroupCoordinator.groups`: members, pending members, static instances, generation | live session state |
| Kafka | `KafkaProducerStates`: per `(ProducerId, Topic)` sequence windows and the next producer id | test data tied to the log |
| Kafka | topics, committed offsets, logs | on the bus already |
| STOMP | none beyond the connection: subscriptions, transactions and outstanding messages die with it (`StompServerConnection`) | - |
| NATS | none beyond the connection; `NatsServer.lastClientId` is a connection counter, not content | - |

### 2. What reset, snapshot and restore do

- **AMQP 0-9-1 exchange table.** Reset removes every declared exchange and its bindings and returns the table
  to the built-in exchanges (plus, with `--directory`, nothing: see decision 4). Snapshot is an opaque deep copy
  of the table's exchanges and bindings, restorable only into the table that made it; another's is refused with
  `InvalidOperationException`, as ADR-0124 does for stores. Restore replaces the table with it. Open channels
  keep their connections; a declare or publish after the swap is judged against the new table, so a channel
  that published to a now-removed exchange gets the ordinary `404 NOT_FOUND` channel close.
- **MQTT 5 sessions.** Reset drops every session that has no connection (its queued deliveries, unacknowledged
  publishes and subscriptions with it) and every pending Will without publishing it. A connected client's
  session stays, but its queued deliveries and unacknowledged publishes are dropped, not redelivered, exactly
  as ADR-0124 decision 3 says for bus deliveries; its subscriptions on the bus stay as the bus's live
  subscriptions stay. `connectedClients` is untouched: those connections are live. Snapshot keeps the offline
  sessions' subscriptions only, with their queued deliveries emptied; nothing in flight is snapshotted, since
  a snapshot is of contents and the bus snapshot already holds the messages. Restore drops every offline
  session and queue as reset does, then reinstates the snapshot's offline sessions with their subscriptions.
- **Kafka.** Reset empties `KafkaProducerStates` (a producer's next batch is judged as its first, against the
  emptied log) and drops every consumer group; a member's next request is answered `UNKNOWN_MEMBER_ID` and the
  client rejoins, which is what a real coordinator failover looks like. Committed offsets go with the bus.
  Snapshot copies the producer states; restore replaces them and drops the groups. Producer ids drawn from the
  injected identifier source keep their sequence, so a seeded run is repeatable.
- **Everything else in the table's "-" rows** has nothing to reset: it follows the bus on its own.

### 3. How a broker learns of it

Each broker with state registers through ADR-0158 decision 2's seam (`IControllableStore`, BL-1158), from
`ExtendedProtocolServers`, after the bus in the order across stores: content, mail, bus, then the broker
registrations in ordinal order of scheme, then accounts. The broker does **not** watch the bus's own `Reset`
and `Restored` events: an event arrives after the bus is already swapped, cannot take part in a snapshot, cannot
refuse and cannot report a failure naming the store. One `StoreChangedEvent` per registration is published
(`Reset`, `Restored`), as for every store. *This decision's order across stores is superseded; see Amendment 1.*
*Its registration seam, and where it is registered from, are superseded; see Amendment 2.*

### 4. `--directory`

ADR-0124 decision 3's refusal stands: the persisted bus already refuses reset, snapshot and restore, and the
order puts the bus first, so a disk-backed instance refuses before any broker is reached. A broker whose own
state is also persisted (AMQP 0-9-1's `Amqp0ExchangeFiles`) repeats the refusal itself, so a registration used
without the bus still cannot empty a real directory. MQTT 5 and Kafka state is memory only and has no refusal
of its own. Seeding is unaffected. *"The order puts the bus first" is superseded; see Amendment 1.*

### 5. Acted on by

BL-1170 (AMQP 0-9-1), BL-1171 (MQTT 5) and BL-1190 (Kafka, filed by this task). MQTT 3.1.1, AMQP 1.0, STOMP
and NATS need no task: nothing of theirs outlives the bus or the connection.

## Alternatives considered

- **React to the bus's `Reset` event.** Rejected in decision 3: too late, no snapshot, no refusal.
- **Leave broker state alone.** Rejected: the leak this ADR exists to close; a queued message would reach the
  next test.
- **Reset group state only when empty.** Rejected: live members with stale generation would continue against an
  emptied log; a forced rejoin is the honest model and is what real Kafka clients handle.

## Consequences

- A test that resets between tests gets no message, exchange, queued delivery or sequence state from the last.
- Three feature tasks follow, each in one broker library and `Surl.Composition`.

## Amendment 1 (2026-11-27, BL-1857): where the broker registrations come in the order across stores

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), in BL-1857. It records the order
the code runs; no code changes. The whole order is in
[ADR-0124's amendment of 2026-11-27](ADR-0124-surl-is-a-stand-in-for-tests.md#amendment-2026-11-27-the-order-across-stores-is-thirteen-stores-bl-1857).

- **Decision 3.** The broker registrations follow the accounts (they do not come between the bus and the accounts),
  in this order: the AMQP 0-9-1 exchanges, the Kafka producers, then the sessions entry, which holds the MQTT 5
  sessions. This replaces "ordinal order of scheme". Other stores the instance holds can come between the accounts
  and these three.
- **Decision 4.** Content comes before the bus, so the order does not put the bus first. With `--directory` the
  content store refuses first, which still stops a whole-instance reset or snapshot before any broker is reached.

## Amendment 2 (2026-11-27, BL-1896): how a broker's state joins `ComposedStores`

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), in BL-1896. It records the seam
the code has; no code changes. The seam is described in
[ADR-0158 Amendment 3](ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md#amendment-3-2026-11-27-bl-1896-how-a-store-joins-composedstores).

This supersedes the `IControllableStore` text of "Builds on" and decision 3: no such interface exists, and no
broker registers from `ExtendedProtocolServers`. In `Surl.Composition`, `Amqp0ExchangeStoreRegistration`,
`KafkaBrokerStoreRegistration` and `Mqtt5SessionStoreRegistration` each build a `ComposedStoreRegistration` over the
composed brokers, or none when there is no such broker. `ControlledBrokerAndSessionStores`
(`SurlServerComposition.Start.cs`) lists them for `ComposedStores`, the MQTT 5 one inside the `StoreKind.Sessions`
entry that `SameKindStoreRegistrations.Combine` builds. Decision 3's other rules stand: no broker watches the bus's
events, and each registration's reset or restore publishes its own `StoreChangedEvent`.
