# ADR-0166 — Which client changes to the AMQP 0-9-1 exchange table, the MQTT 5 sessions and the Kafka producer states publish StoreChangedEvent

- **Status:** Accepted
- **Date:** 2026-11-15
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-15, in BL-1215.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) (decision 1: every store change is published, events
  never block, no secret in an event; decision 4: `out-of-space` at `FaultStage.StoreWrite`),
  [ADR-0158](ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md)
  (decision 2's event shape, decision 7's precedent: ask the plan before a write that may grow the store) and ADR-0003.
- **Amends:** [ADR-0159](ADR-0159-which-broker-held-state-a-reset-snapshot-and-restore-of-the-message-bus-cover.md)
  (its classes "test data" and "live session state" now say what is published) and ADR-0124 decision 1 (the list of
  `StoreChange` members grows by two).
- **Acted on by:** the task listed in Notes of BL-1215 (the AMQP 0-9-1 broker).

## Context

ADR-0159 registered three broker-held stores that publish only `Reset` and `Restored`. A client's changes to them
publish nothing: the AMQP 0-9-1 broker's only `Publish` is a `FaultAppliedEvent`. A test that declares an exchange
over AMQP and asserts on the event stream sees nothing. Each broker's code was read for this decision.

## Decision

### 1. Which client actions are store changes

A client action is a **store change** when it changes what a *later, different connection* finds, and the change
is not already published by the bus. Otherwise it is live session state or a duplicate and publishes nothing.

| Store | Client action | Store change? |
| --- | --- | --- |
| Exchanges (AMQP 0-9-1) | `exchange.declare` that creates an exchange | yes |
| Exchanges | `exchange.declare` of an existing, equivalent exchange (passive, or same type and flags) | no: nothing changed |
| Exchanges | `exchange.delete`, and an auto-delete exchange going when its last binding goes | yes |
| Exchanges | `queue.bind` and `exchange.bind` that add a binding the table did not hold | yes |
| Exchanges | `queue.unbind` and `exchange.unbind` that remove one, and every binding a deleted exchange takes with it | yes |
| Exchanges | a bind of a binding the table already holds, an unbind of one it does not | no |
| Exchanges | a bind or unbind against the default topic space (`BindOnTopicSpace`): these are bus bindings | no: the bus publishes them |
| Sessions (MQTT 5) | connect, resume, subscribe, unsubscribe, queued delivery, acknowledgement, Will, session expiry, takeover | no: live session state (ADR-0159); the messages and subscriptions that matter are already `Bus` events |
| Producers (Kafka) | `InitProducerId`, a producer's sequence window advancing, a window evicted at `DefaultMaxProducerStates` | no: bookkeeping derived from the log, whose appends the bus publishes; one event per batch would double-count every produce |

So **Sessions** and **Producers** keep publishing only `Reset` and `Restored`. No task is needed for them.

### 2. `StoreChange` member and `Name`

`StoreChange` grows by **`Bound`** (a binding was added) and **`Unbound`** (one was removed), appended after the
existing members; nothing is renumbered. (The enum only grows.) Exchanges reuse existing members:

| Change | `Change` | `Name` | `ByteCount` | `ExchangeId` |
| --- | --- | --- | --- | --- |
| exchange created | `Created` | the exchange name (UTF-8, truncated at 256 characters as ADR-0158 decision 2) | null | the connection's exchange |
| exchange deleted | `Deleted` | the exchange name | null | the exchange, or null for an auto-delete |
| binding added | `Bound` | `<source exchange>/<destination>/<routing key>` - the destination is the queue name, or the destination exchange for `exchange.bind` | null | the connection's exchange |
| binding removed | `Unbound` | the same | null | the exchange, or null when a delete took it |

A deleted exchange publishes one `Unbound` per binding it held, **then** its `Deleted`, so a subscriber never sees
a binding of an exchange it was told is gone. `Name` is for display and assertions by prefix; a name containing
`/` is ambiguous and a test that needs exact parts reads the table. Arguments of a binding (a headers match) and
the exchange's `alternate-exchange` and other arguments are never published. No secret is involved: an exchange
name, queue name and routing key are not credentials.

### 3. `out-of-space` at an exchange declare

ADR-0158 decision 7's rule applies: a store gaining a client write asks the plan before a write that may grow it.
At `FaultStage.StoreWrite` the broker asks, with `FaultPoint { Stage = StoreWrite, Path = <exchange name>,
Account = the authenticated user or null }`, before an `exchange.declare` that would **create** an exchange and
before a bind that would **add** a binding (`Path` is the source exchange name). Redeclaring, deleting and
unbinding shrink or keep the store and are not asked. A matching `OutOfSpace` rule makes the call write nothing
and answer as the broker answers its own bound (`TooManyExchanges`): connection close `RESOURCE_ERROR` (reply code
506). The rule is published as a `FaultAppliedEvent` as every fault is. MQTT 5 sessions and Kafka producer
states publish nothing, so they are asked nothing: they have no client write to refuse.

### 4. Seeds and reset

A seed and a reset go through the registration's existing path and publish as ADR-0159 says; a seed of
exchanges, if one is ever offered, publishes `Seeded` per exchange and never consults the plan (ADR-0158
decision 3).

## Alternatives considered

- **Publish every MQTT 5 session and Kafka producer-state change.** Rejected: the first is connection lifetime, the
  second is a per-batch counter; both would flood the stream and carry nothing a test cannot read from the bus.
- **One `Written` for an exchange and a binding.** Rejected: a subscriber could not tell them apart without parsing
  `Name`; two new members cost nothing and the enum only grows.
- **Publish a binding as part of the exchange's event.** Rejected: one event per item changed (ADR-0158).

## Consequences

- A test sees `Created`, `Bound`, `Unbound` and `Deleted` for AMQP 0-9-1 topology on the event stream and in
  `Surl.Mcp`, and can fault an exchange declare with `out-of-space`.
- One feature task follows, in the AMQP 0-9-1 broker library, `Surl.Protocol.Abstractions` (the two members) and
  `Surl.Composition` only if the registration needs it.
