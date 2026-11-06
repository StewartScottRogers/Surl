# ADR-0146 — How the AMQP 1.0 container serves transactions, durable subscriptions and source filters

- **Status:** Accepted
- **Date:** 2026-11-06
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06,
  in BL-707. BL-954 gives `Surl.MessageBus` an all-or-none batch publish (decision 1.6), BL-955 a
  filtered queue consumer and a subscription description on a named queue (decisions 2.4 and 3.2),
  BL-956 builds the coordinator, `declare`, `discharge` and transactional posting (decisions 1.1 to
  1.4 and 1.6 to 1.8), BL-957 transactional retirement and acquisition (decision 1.5), BL-958 the
  JMS selector (decision 2.2), BL-959 the source filters (decisions 2.1, 2.3 and 2.4), and BL-960
  durable and shared topic subscriptions (decision 3).
- **Builds on:** [ADR-0107](ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md) (the
  container, its nodes, links, transfers and settlement),
  [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 3 (the shared message bus, its queues, bounds and persistence),
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decision 3.4 (topic access), [ADR-0112](ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md)
  decision 6 (AMQP 0-9-1's transactions, the nearest precedent), [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md)
  (every store a peer can fill is bounded) and [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md)
  (the stand-in: seed, reset, snapshot, restore, determinism).
- **Amends:** ADR-0107 decision 5's "Not served yet" bullet, which this ADR replaces once BL-956,
  BL-959 and BL-960 are built; until each is, ADR-0107's interim answer stands for its item.
  ADR-0107 decision 4's refusal of a `coordinator` target (matrix row `Amqp1:Surl-Nodes-6`) ends
  with BL-956. ADR-0100 decision 3.3's bus gains three operations (decisions 1.6, 2.4 and 3.2).
  The amendments are recorded here only, because BL-707 touches only this folder and
  `Documentation/Conformance/Extended/Amqp1.md`; the earlier ADRs point forward when their builders
  edit them.

## Context

ADR-0107 decision 5 left three parts of AMQP 1.0 "not served yet": transactions, durable topic
subscriptions and source filters. Until now the container refuses a `coordinator` target with
`amqp:not-implemented`, answers a topic source that asks for durability with `durable` `none`, and
leaves a client's `filter` map out of its answering source, which Part 3 section 3.5.3 defines as
"those filters were not applied" - the client then decides whether to go on.

A test suite using Surl as a stand-in broker (root `CLAUDE.md`, "Surl is a stand-in for tests")
meets all three: a JMS client over AMQP 1.0 (Apache Qpid JMS, the ActiveMQ Artemis and Azure Service
Bus clients) opens a transacted session with `Session.SESSION_TRANSACTED`, creates a durable
subscriber with `createDurableSubscriber`/`createSharedDurableConsumer`, and passes a message
selector to `createConsumer`. Each of those, against today's container, fails or silently gets
different behaviour.

As in ADR-0107, the container is judged by its specification (ADR-0083 decision 1), never by the
Curl port, whose client sends none of the three. The specification read is **OASIS AMQP Version
1.0, 29 October 2012** (below "Part n"):

- Part 4, transactions: 4.1 transactional messaging, 4.2 declaring a transaction, 4.3 discharging a
  transaction, 4.4 using a transaction (4.4.1 transactional posting, 4.4.2 transactional retirement,
  4.4.3 transactional acquisition, 4.4.4 interaction of settlement with transactions), 4.5
  transaction coordination (4.5.1 `coordinator`, 4.5.2 `declare`, 4.5.3 `discharge`, 4.5.4
  `transaction-id`, 4.5.5 `declared`, 4.5.6 `transactional-state`, 4.5.7 `txn-capability`, 4.5.8
  `transaction-error`);
- Part 3 section 3.5: 3.5.1 filtering messages, 3.5.3 `source` (its `durable`, `expiry-policy`,
  `timeout`, `filter` and `capabilities` fields), 3.5.5 `terminus-durability`, 3.5.6
  `terminus-expiry-policy`, 3.5.8 `filter-set`;
- Part 2 section 2.6.6 (detaching: `closed` true destroys the terminus) and section 2.8.15 (the
  error conditions, `amqp:resource-locked` among them).

Two convention documents fill what the specification leaves to registries, read as conventions and
never as verdicts:

- the **AMQP filter registry** kept by Apache Qpid (`apache-filters.xml`), which names the filter
  types and descriptors: `apache.org:legacy-amqp-direct-binding:string` (`0x0000468C:0x00000000`),
  `apache.org:legacy-amqp-topic-binding:string` (`0x0000468C:0x00000001`),
  `apache.org:legacy-amqp-headers-binding:map` (`0x0000468C:0x00000002`),
  `apache.org:no-local-filter:list` (`0x0000468C:0x00000003`) and
  `apache.org:selector-filter:string` (`0x0000468C:0x00000004`);
- **Jakarta Messaging 3.1** section 3.8.1 (message selector syntax, the SQL-92 subset JMS
  selectors use) and the OASIS **AMQP JMS Mapping** working draft for which AMQP field each JMS
  header name reads.

The bus (ADR-0100 decision 3.3) has named queues (durable, exclusive, auto-delete, bound to
filters, persisted when durable and neither exclusive nor auto-delete) and competing consumers,
but no batch publish, no consumer that takes only some messages, and nothing kept beside a queue
but its flags and bindings.

## Decision

### 1. Transactions (Part 4)

1. **The coordinator (4.5.1, 4.5.7).** An `attach` whose target is a `coordinator` (descriptor
   `amqp:coordinator:list`, `0x30`) is answered as any receiving link (ADR-0107 decision 4: role
   receiver, credit 100), its target a `coordinator` whose `capabilities` are the ones the container
   serves: **`amqp:local-transactions`**, **`amqp:multi-txns-per-ssn`** and
   **`amqp:multi-ssns-per-txn`**. `amqp:distributed-transactions` and
   `amqp:promotable-transactions` are left out when asked, so the client decides whether to go on,
   as Part 3 section 3.5.3 has it for filters. A coordinator needs no topic access: each message a
   transaction posts is judged when it is posted (decision 1.4).
2. **Scope.** A transaction belongs to the **connection**: any coordinator link and any link on any
   session of that connection may use it (hence `multi-ssns-per-txn`), and a session may run
   several at once (`multi-txns-per-ssn`). A transaction id from another connection is unknown.
3. **Declare (4.2, 4.5.2, 4.5.5).** A transfer on a coordinator link whose body is one
   `amqp-value` holding a `declare` (`0x31`) is answered with a `disposition` whose state is
   `declared` (`0x33`) carrying the new `txn-id`. Ids are **4-byte binaries counting up from
   `00 00 00 01` per connection**, never reused on it, so a test sees the same ids every run
   without seeding. A `declare` with a `global-id` (a distributed transaction) is answered
   `rejected` with `amqp:not-implemented`. More than **100 open transactions** on one connection
   (ADR-0006) is answered `rejected` with `amqp:resource-limit-exceeded`. Any other body on a
   coordinator link is answered `rejected` with `amqp:decode-error`.
4. **Transactional posting (4.4.1).** A transfer to a receiving link whose `state` is
   `transactional-state` (`0x34`) naming an open transaction is judged at once as ADR-0107 decision
   6 judges any transfer (format, size, topic access, address). A transfer it would refuse is
   answered at once with `transactional-state` whose outcome is `rejected` with that error, and is
   not part of the transaction. Otherwise the message is **held in the transaction, not published**,
   and the container answers `transactional-state` with outcome `accepted` - the provisional
   outcome - settling it per its `rcv-settle-mode` `first`. Held messages count, with the
   connection's partial deliveries, against `--max-filesize` (ADR-0107 decision 6); past it the
   transfer is answered `rejected` with `amqp:resource-limit-exceeded`. A `txn-id` naming no open
   transaction is answered `rejected` with **`amqp:transaction:unknown-id`**, settled, outside any
   transaction.
5. **Transactional retirement and acquisition (4.4.2, 4.4.3, 4.4.4).** A client `disposition`
   whose state is `transactional-state` holds its outcome (`accepted`, `rejected`, `released`,
   `modified`) in the transaction: the delivery stays acquired by its link and is neither
   redelivered nor dropped until the transaction is discharged. A `flow` whose `properties` map
   carries `txn-id` makes the deliveries the container then sends on that link carry
   `transactional-state` naming that transaction, until a `flow` without it: those deliveries are
   acquired in the transaction, and a rollback returns them to their queue (marked redelivered)
   unless the link retired them outside it. Topic deliveries have nothing to return: their
   outcomes only settle them (ADR-0107 decision 6), in or out of a transaction.
6. **Discharge (4.3, 4.5.3, 4.5.8).** A transfer on a coordinator link holding a `discharge`
   (`0x32`):
   - with **`fail` true** rolls the transaction back: held messages are dropped, held outcomes are
     forgotten (their deliveries acquired and unsettled again, as AMQP 0-9-1's rollback puts them
     back outstanding), acquired deliveries return as decision 1.5 says; answered `accepted`;
   - with **`fail` false** commits it: the held messages are published **all or none** through the
     bus's batch publish (BL-954), in the order they were posted, then the held outcomes are applied
     in order; answered `accepted`. If the bus refuses any message of the batch (`StoreFull`,
     `QueueFull` sending straight to a queue, `TooManyTopics`), it publishes none of them, the
     transaction is rolled back as with `fail` true, and the `discharge` is answered `rejected` with
     **`amqp:transaction:rollback`**;
   - naming no open transaction, `rejected` with **`amqp:transaction:unknown-id`**.

   A discharged transaction's id is never valid again on the connection.
7. **Ending.** A transaction is rolled back, with no frame of its own, when the coordinator link
   that declared it detaches, when that link's session ends, and when the connection ends. There is
   no transaction time-out: `amqp:transaction:timeout` is never sent, and the 100-transaction and
   `--max-filesize` bounds keep what a transaction holds finite.
8. **Pre-settled control messages.** A `declare` or `discharge` the client sends pre-settled is
   still applied; per Part 2 section 2.6.12 nothing answers it, so the client never learns the id
   or the result, which is its choice.

### 2. Source filters (Part 3 sections 3.5.1, 3.5.3, 3.5.8; the filter registry)

1. **Which filters are applied.** An entry of a source's `filter` map is recognised by its
   **descriptor** (symbol or ulong form; its key is the client's own name and is echoed). The
   container applies:
   - `apache.org:selector-filter:string` - a JMS selector (decision 2.2), on topic and queue links;
   - `apache.org:no-local-filter:list` - on a **topic** link, messages published by the same
     connection are not delivered, through the bus's `noLocal` subscription; on a queue link it is
     not applied (JMS defines no-local for topics only);
   - `apache.org:legacy-amqp-direct-binding:string` - the message's routing key equals the value;
   - `apache.org:legacy-amqp-topic-binding:string` - the routing key matches the value as an AMQP
     0-9-1 topic pattern (`.`, `*`, `#`, the rules `AmqpTopicExchangeNames` already holds);
   - `apache.org:legacy-amqp-headers-binding:map` - the message's application properties match the
     map under its `x-match` (`all`, the default, or `any`), as the AMQP 0-9-1 headers exchange
     does (ADR-0112).

   A message's **routing key** is its `subject` when it has one, else its bus topic written as
   `AmqpTopicExchangeNames` writes it (`/` becomes `.`), so a message another protocol published is
   routed as AMQP 0-9-1 would route it; a message sent straight to a queue with no `subject` has no
   routing key and matches neither binding. Several applied filters must **all** match. Every
   applied entry is echoed in the container's source under the client's key; an entry with any
   other descriptor (`apache.org:xquery-filter:string` among them) is left out, so the client
   decides, as section 3.5.3 has it. A filter map on a **target** is not part of AMQP and is
   ignored.
2. **The JMS selector** (Jakarta Messaging 3.1 section 3.8.1) is parsed and evaluated by a
   hand-built recogniser in `Surl.Extended.Protocol.Amqp1` (BL-958): literals (strings with `''`,
   exact and approximate numbers, `TRUE`, `FALSE`), identifiers, parentheses, `NOT`, `AND`, `OR`,
   `=`, `<>`, `<`, `<=`, `>`, `>=`, `+`, `-`, `*`, `/`, `[NOT] BETWEEN`, `[NOT] IN`,
   `[NOT] LIKE ... [ESCAPE ...]` and `IS [NOT] NULL`, keywords in any case, with SQL's three-valued
   logic: an identifier the message lacks is NULL, and a message is selected only when the
   expression is TRUE. Identifiers read, per the AMQP JMS Mapping draft: `JMSMessageID` the
   `message-id`, `JMSCorrelationID` the `correlation-id`, `JMSType` the `subject`, `JMSPriority`
   the header's `priority` (default 4), `JMSDeliveryMode` `'PERSISTENT'` when the header's
   `durable` is true else `'NON_PERSISTENT'`, `JMSTimestamp` the `creation-time` and
   `JMSExpiration` the `absolute-expiry-time`, both as milliseconds; any other identifier the
   application property of that name. For a message another protocol published, these read the
   fields ADR-0107 decision 6 maps it to (headers as application properties). A selector longer than
   **4096 characters**, nested deeper than **32** levels, or that does not parse refuses the
   attach (ADR-0107 decision 4: a null source, then a closing `detach`) with **`amqp:invalid-field`**
   and fixed text; an empty selector selects every message and is echoed.
3. **Answering.** The container's source carries the applied filters, its `default-outcome` and
   `distribution-mode` as ADR-0107 decision 5 sets them.
4. **Where filters run.** On a topic link the container evaluates the filters on each message the
   subscription hands it, before it waits for credit (ADR-0107's `Amqp1:Surl-Delivery-1`), so a
   message filtered out never counts against the 1000 waiting. On a queue link the filters go to the
   bus as a **consumer predicate** (BL-955): a consumer is offered only messages its predicate
   accepts, a message no consumer accepts stays ready in the queue for one that will, and round
   robin runs among the consumers that accept it. The bus calls the predicate under its lock, so it
   must be pure and quick; a selector is both.

### 3. Durable and shared topic subscriptions (Part 3 sections 3.5.3, 3.5.5, 3.5.6; Part 2 section 2.6.6)

1. **What a durable subscription is.** A receiving link's source on a `/topic/` address with
   `durable` `configuration` (1) or `unsettled-state` (2) and `expiry-policy` `never` makes a
   **durable subscription**: a bus named queue, durable and neither exclusive nor auto-delete, bound
   to the topic filter, which keeps every matching message while no link is attached and so is
   persisted under `--directory`, reset, snapshotted and restored with the bus (ADR-0100 decision
   3.7, ADR-0124). It is named `surl-subscription-` and the lowercase hex of the SHA-256 of the
   client's `container-id`, a zero byte and the link's `name` - stable, any length of either fits,
   and no client text reaches a queue name. The container answers `durable` **`configuration`**:
   the terminus is kept but its unsettled state is not, because links are never resumed (ADR-0107,
   `Amqp1:Core-2.6.13-1`); deliveries unsettled when the link detaches go back to the subscription's
   queue, marked redelivered. `timeout` is answered as asked; it is moot with `never`.
2. **What the bus keeps beside it.** The subscription's queue carries a **subscription description**
   (BL-955): the source's address and the canonical encoding of its applied filter entries, kept,
   persisted and snapshotted with the queue, and readable without consuming. Filters apply when a
   message is taken from the queue, not when it is copied in, so a durable subscriber's queue fills
   with every matching topic message and drops at delivery those its filters refuse (acknowledging
   them, since the queue is the subscription's alone). Its bounds are any queue's (ADR-0100
   decision 3.6): past `TooManyQueues` the attach is refused with
   `amqp:resource-limit-exceeded`.
3. **Reattaching.** An attach with the same `container-id` and link name finds the subscription:
   - with the **same address and filters**, it resumes it and receives what was kept;
   - with a **different address or filters**, the subscription is replaced - its queue deleted with
     its messages and a new one declared - as Jakarta Messaging 3.1 has a durable subscription
     whose topic or selector changes be unsubscribed and created anew;
   - with **no source** (`source` null, which a JMS client sends to unsubscribe), it is answered with
     the subscription's own source (address, `durable`, `expiry-policy`, the applied filters); with
     none of that name it is refused as today (`amqp:not-found`).

   A second attach to a subscription another link holds is refused with **`amqp:resource-locked`**
   (Part 2 section 2.8.15), unless the subscription is shared (decision 3.5).
4. **Ending one.** A `detach` with `closed` true destroys the terminus (Part 2 section 2.6.6): the
   subscription's queue is deleted with its messages. A `detach` with `closed` false, the session's
   end or the connection's end leaves it. An address naming a `surl-subscription-` queue directly
   (`/queue/surl-subscription-...`) is refused with `amqp:not-found`, so only the subscriber reaches
   it.
5. **Shared subscriptions.** A source whose `capabilities` include **`shared`** names a subscription
   several links consume at once, competing as queue consumers do (ADR-0100 decision 3.3): its key
   is the link name up to the first `|` (the suffix a JMS client adds to tell the links of one shared
   subscription apart), with the `container-id` - or without it when the capabilities also include
   **`global`**. The container echoes the capabilities it applied. A shared subscription is
   destroyed by a `closed` detach only when it is the last link attached.
6. **The shorter expiry policies.** With `durable` above `none`, `expiry-policy` **`session-end`**
   or **`connection-close`** keeps the subscription - a bus queue exclusive to the connection and
   not persisted - until that event, so a link detached with `closed` false and attached again under
   the same name in the same session or connection resumes it; `link-detach` is the same as
   `durable` `none`. The container answers the policy it applied, `durable` `configuration` for the
   first two and `none` for `link-detach`, and `timeout` 0: it removes the terminus at the event
   itself.
7. **Who may.** A durable or shared subscription needs the `Subscribe` right on its topic filter
   under `--topic-access` (ADR-0102 decision 3.4), judged at every attach, resumed ones included;
   refused, the attach gets `amqp:unauthorized-access` as ADR-0107 decision 4 says.

## Alternatives considered

- **Keeping the interim answers.** Part 3 section 3.5.3 lets a container leave filters out and
  answer `durable` `none`, and refusing a coordinator is legal. But a JMS stand-in that ignores
  selectors hands the test messages it filtered out, and one without transactions fails a
  transacted session outright; root `CLAUDE.md` says nothing is left out because it is hard.
- **Committing in order without the bus's batch, as AMQP 0-9-1's `tx` does (ADR-0112 decision 6).**
  The 0-9-1 `tx` class says plainly that nothing is atomic across queues, so its broker may publish
  part of a commit. Part 4 makes no such allowance: a discharge either commits the transaction or
  answers `amqp:transaction:rollback`. A commit that published half its messages and then said
  "rolled back" would lie to the test, so the bus gains an all-or-none batch.
- **Transaction ids from `ExchangeContext.Identifiers` (random, seedable).** A per-connection
  counter is deterministic with no seed and cannot collide on its connection, which is the id's
  whole scope (decision 1.2).
- **Evaluating selectors in the bus.** The bus is protocol-neutral and knows nothing of JMS headers
  or AMQP properties; a predicate keeps the selector in the AMQP 1.0 library and the bus generic.
- **Filtering a durable subscription when a message is copied into its queue.** That would need the
  bus to call a protocol's code on every topic publish, even with no subscriber attached, and to
  persist something it can evaluate after a restart. Filtering at delivery keeps the bus generic at
  the cost of holding messages the filters will drop, which the queue's bounds already limit.
- **Naming the subscription queue from the container id and link name in clear.** Both are client
  text of any length and alphabet; a hash keeps the bus's queue-name rules out of the client's
  hands.

## Consequences

- `Documentation/Conformance/Extended/Amqp1.md` gains rows for Part 4, the filter set and the
  terminus fields, each naming its building task; `Amqp1:Surl-Nodes-6` changes from the refusal to
  BL-956's coordinator.
- `Surl.MessageBus` grows three operations, each useful beyond AMQP 1.0: a batch publish (a
  STOMP or Kafka transaction can use it), a consumer predicate (any selector), and a description kept
  with a named queue (any durable subscription).
- A stand-in test can now run a transacted JMS session, a durable or shared subscriber and a
  selector against Surl, and inspect a durable subscription's queue through `Surl.Api` like any
  other.
