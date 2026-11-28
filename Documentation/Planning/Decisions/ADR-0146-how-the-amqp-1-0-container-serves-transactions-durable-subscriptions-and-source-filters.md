# ADR-0146 — How the AMQP 1.0 container serves transactions, durable subscriptions and source filters

- **Status:** Accepted
- **Date:** 2026-11-06
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06,
  in BL-707. BL-961 gives `Surl.MessageBus` an all-or-none batch publish (decision 1.6), BL-955 a
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
     bus's batch publish (BL-961), in the order they were posted, then the held outcomes are applied
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

## Amendment 1 (BL-957): details of decision 1.5

Decided by Claude under Stewart's delegation.

- A `disposition` in `transactional-state` naming no open transaction is answered with a `disposition`
  from the container (sender role, same range) whose state is `rejected` with
  `amqp:transaction:unknown-id`. It is not settled and changes nothing: the deliveries stay as they
  were, so the client may retire them again outside any transaction.
- A client that settles in the transactional `disposition` has the delivery removed from the link's
  unsettled set at once; a rollback puts it back (unless its link has ended), and a commit applies the
  outcome. Under `rcv-settle-mode` `second`, a commit settles the deliveries the client left unsettled.
- A later `disposition` for a delivery replaces the outcome held for it; one outside any transaction
  drops the held outcome and applies as ADR-0107 decision 5 says.
- A rollback returns every queue delivery acquired in the transaction that is still unsettled on its
  link, including one retired inside the transaction, to its queue marked redelivered. A delivery the
  link settled outside the transaction stays retired. A `flow` naming a `txn-id` that is not open
  acquires nothing.

## Amendment 2 (BL-2027, 2026-11-27): who may delete or purge a subscription queue by name

Decided by Claude under Stewart's delegation.

Decision 3.7 gives a subscription to whoever has `Subscribe` on its topic filter, but the bus's
delete and purge of a named queue judged no grant, so any client that knew or derived a
`surl-subscription-` name could remove another account's kept subscription, for example with AMQP
0-9-1's `queue.delete`.

- `Surl.MessageBus`'s delete and purge by name, on a queue carrying a subscription description, need
  `sub` covering **every** filter the queue is bound to; otherwise they are `AccessDenied` (AMQP 0-9-1
  answers `403`), and the queue, its bindings and its messages stay.
- A described queue bound to **no** filter is refused whenever any `--topic-access` grant is
  configured: there is nothing to judge, and secure by default refuses it. With no grant, everyone may.
- A queue with no description is deleted and purged as before.
- The AMQP 1.0 container's own deletes (`DeleteQueueIfDescribed`: ending a subscription, replacing it,
  and a session's end) ask for no grant: the container judges access at the attach (decision 3.7), and a
  session's end must remove what it made.

Alternatives: asking for `$queue/<name>` rights would lock out the subscriber, who needs no queue grant
(decision 3.7); refusing every delete by name would take away the clean-up an account with the filter's
right may reasonably do. The check reads the bindings as they are at the delete, and an unbind asks for
no grant, so an account with `sub` on another filter can still swap the bindings first; keeping other
clients' bindings off a described queue is BL-2011.

## Amendment 3 (BL-2037, 2026-11-27): no-local on a kept subscription

Decided by Claude under Stewart's delegation.

Decision 2.1 withholds, on a topic link, what "the same connection" publishes, and decision 3 says
nothing about it. A kept subscription can outlive its connection, and its queue keeps no publisher.
Read for this: **Jakarta Messaging 3.1**.

- Section 8.3.3 (unshared durable subscriptions): "The noLocal parameter may be used to specify that
  messages published to the topic by the Session, JMSContext or TopicSession's own connection, or any
  other connection with the same client identifier, will not be added to the durable subscription."
  The same section makes a resubscribe with a different `noLocal` value "equivalent to unsubscribing
  (deleting) the old one and creating a new one".
- Section 8.3.1 (unshared non-durable subscriptions) speaks of "its own connection" only.
- Appendix A.3.8.1 and A.3.8.2: `noLocal` "is not supported" for shared non-durable or shared durable
  subscriptions; sections 8.3.2 and 8.3.4 give it no meaning.

This amends decision 2.1 for kept subscriptions (decisions 3.1, 3.5 and 3.6). BL-2038 builds the bus
part and BL-2039 the container part; until both are done a kept subscriber is echoed the filter and
still receives its own publishes.

1. **Which links.** A link served as a live topic subscription is unchanged: the bus's `noLocal`
   subscription withholds what its own connection publishes. That includes a link asking `shared`
   with `durable` `none` or `expiry-policy` `link-detach`, which the container does not serve as
   shared. A kept subscription that is not shared, under any of `never`, `session-end` and
   `connection-close`, applies no-local as items 2 to 7 say.
2. **Whose publishes.** The client is identified by the `container-id` of its `open` (Part 2 section
   2.7.1), which stands where JMS has the client identifier and already keys the subscription
   (decision 3.1). A kept subscription with no-local is withheld every topic publish made by an AMQP
   1.0 connection of this instance whose `open` carried the subscriber's `container-id`: the
   subscribing connection, any other open at the same time, and any later one, **whether or not a link
   is attached**, and across a restart for `never`. Every other publish reaches it: another
   `container-id`, another protocol, the test host's own publish and a seed. The account plays no
   part; a client that gives another's `container-id` only keeps its own messages from that
   subscriber.
3. **Where.** The bus withholds at **copy-in**: the message is never put in the queue, so it counts
   against no queue bound. The withheld copy raises no store change, counts in none of `QueuedCount`,
   `FullQueueCount` and `UnconsumedQueueCount`, and the out-of-space fault is not asked for it. The
   publish otherwise behaves as before: logs, retained slot, live subscriptions and other queues take
   it, and the publisher is answered `accepted`.
4. **What `Surl.MessageBus` adds (BL-2038).**
   - `BusConnection.PublisherName`: a settable, nullable, opaque name of the client publishing through
     the connection, `null` unless a server sets it. The AMQP 1.0 container sets it, once the client's
     `open` is read, to `amqp1:` followed by the `container-id`, so another protocol that names its
     publishers prefixes its own scheme family and never matches.
   - An `excludedPublisherName` parameter on a `BusConnection.DeclareQueue(name, flags,
     subscriptionDescription, excludedPublisherName)` overload: a topic publish through a connection
     whose `PublisherName` equals it, ordinally, is not copied into the queue. A publish with no
     connection or no publisher name is never withheld. It needs a subscription description (an
     `ArgumentException` without one). It is set when the queue is declared, before it is bound, so no
     publish slips in. A redeclare that passes every other redeclare check, the description matching,
     sets it on the existing queue, so an unshared subscription persisted before BL-2038 gains it when
     its subscriber next attaches (what is already queued stays). A declare passing `null` changes
     nothing.
5. **Persisted and snapshotted.** The excluded publisher name is kept beside the description:
   persisted with a `never` subscription, and snapshotted and restored with its queue; it goes with a
   queue that a reset or delete removes, and stays with a queue a reset keeps. When some persisted
   queue has one, `queues/index` is written as `SURL-BUS-QUEUES-3`, where every queue's description
   field is followed by `0x00`, or `0x01` and the excluded publisher name. Otherwise it is written as
   before, and `SURL-BUS-QUEUES-1` and `SURL-BUS-QUEUES-2` files still load.
6. **A transaction's batch** is published through the committing connection, so its topic entries are
   withheld exactly as single publishes are, and the batch's bound check counts no withheld copy.
7. **Reattaching** needs no new rule: no-local is among the applied filters the description holds
   (decision 3.2), so the same filters resume the subscription, and adding or dropping no-local
   replaces it (decision 3.3), as section 8.3.3 says. A lookup attach is answered the filters the
   container applies to the subscription it found (for a shared one, see item 8).
8. **A shared subscription** (decision 3.5) does **not** apply no-local, `global` or not, durable or
   not durable: the entry is left out of the echoed source and of the description, so the client
   decides whether to go on (Part 3 section 3.5.3), and the queue excludes nobody. A shared
   subscription an earlier build persisted with the entry in its description differs from what is now
   planned, so its next attach with a source replaces it.
9. **The echo** therefore names no-local exactly where it is applied: a live topic link, and a kept
   subscription that is not shared. A queue link and a shared subscription leave it out.

### Alternatives considered (Amendment 3)

- **The subscribing connection only**, as for a live link. A `never` subscription outlives it, so a
  client resuming would receive what it published on a later connection, against section 8.3.3.
- **Only while a link is attached.** Section 8.3.3 says such messages are not added to the
  subscription, attached or not.
- **Withholding at delivery**, as filters run (decision 2.4). Every queued message would have to keep
  its publisher in memory, in its file and in snapshots, and messages that are never to be delivered
  would use the queue's bounds.
- **A predicate the bus calls at copy-in.** That is the cost "Alternatives considered" above rejects
  for selectors: protocol code run on every topic publish, and a rule the bus must persist and still
  evaluate after a restart. Comparing two opaque names has neither cost: the bus stays
  protocol-neutral and calls no protocol code, and the name is a string in its files.
- **Identifying the client by account.** Anonymous clients would all be one client, and JMS names the
  client identifier.
- **Own connection only for `session-end` and `connection-close`.** It needs a second bus rule for
  queues that only the same `container-id` can resume anyway.
- **Applying no-local to a non-`global` shared subscription by `container-id`**, or to any shared one
  by whoever declared it. Jakarta Messaging gives it no meaning, a `global` one has no single client,
  and the first declarer would make the answer depend on attach order.
- **Refusing a shared attach that asks for it.** Part 3 section 3.5.3 has a container leave out what it
  does not apply.
- **Leaving it out of every kept subscription.** Honest, but a JMS durable subscriber asking
  `noLocal` is a real use of the stand-in, and root `CLAUDE.md` says nothing is left out because it is
  hard.

## Amendment 4 (BL-2040, 2026-11-27): how a non-durable shared subscription is held

Decided by Claude under Stewart's delegation.

Decision 3.5 does not limit `shared` to durable sources, and a JMS `createSharedConsumer` is
non-durable, but decision 3 does not say how such a subscription is held. Until BL-2041 builds this
amendment, each such link is its own live subscription: every link gets every message and no capability
is echoed. Read for this: **Jakarta Messaging 3.1** section 8.3.2 (shared non-durable subscriptions): a
subscription is identified by its name and the client identifier if set; it exists only while a consumer
is active on it; it is deleted, with its undelivered messages, when its last consumer is closed; and an
additional consumer asking another topic or message selector while one is active is an exception.
Sections 8.3.3 and 8.3.4 allow a durable subscription and a shared non-durable one the same name, and
8.3.3 calls them completely separate.

1. **Which links.** A client receiver's **topic** source whose `capabilities` include `shared` and that
   asks no kept subscription: `durable` `none` (whatever its `expiry-policy`), or any `durable` with
   `expiry-policy` `link-detach`. A queue link is unchanged and echoes no capability, since a queue's
   consumers already compete. `global` without `shared` shares nothing.
2. **What holds it.** A bus named queue with the flags **auto-delete** only (not durable, not exclusive),
   carrying a subscription description (decision 3.2), bound to the topic filter, and consumed by every
   link through `ConsumeSubscriptionQueue`, not exclusively, with the 1000 unacknowledged of any queue
   link. So links of one connection or of several compete, each message goes to one of them, and what a
   link leaves unsettled or waiting when it ends goes back to the queue for the others, marked
   redelivered, as `released` and `modified` outcomes put a delivery back. The queue's bounds
   replace a live topic link's drop past 1000. Filters run as a message leaves the queue, one they
   refuse being acknowledged (decision 3.2). `Surl.MessageBus` needs no change.
3. **Name.** `surl-subscription-nondurable-` and the lowercase hex of the SHA-256 of decision 3.5's key:
   the link name up to the first `|`, after the `container-id` (empty under `global`) and a zero byte. No
   kept subscription's name, 64 hex digits after `surl-subscription-`, can equal it, so a non-durable
   attach never joins, replaces or deletes a durable subscription of the same link name. It keeps the
   `surl-subscription-` prefix, so its `/queue/` address is refused (decision 3.4), and it takes no
   direct send. The description records the policy `link-detach` whatever the client asked, so links
   differing only in the `durable` or `expiry-policy` they asked share one subscription.
4. **Other filters.** While a link consumes from it, an attach under the name with another address or
   other applied filters is refused with **`amqp:resource-locked`** (`subscription in use`), as section
   8.3.2 has it. A queue found under the name with another description and no consumer is replaced
   (decision 3.3).
5. **Ending.** The bus's auto-delete removes the queue, with its messages, when its last consumer ends:
   a `detach`, `closed` or not, the session's end, the connection's end or the container's own detach.
   The container asks for no delete then and records nothing for the session's end. One of several links
   ending leaves it to the others. An attach refused after the container declared the queue deletes it
   with `DeleteQueueIfDescribed(name, description, ifUnused: true)`, so nothing is left behind. When the
   bind or the consume answers `UnknownQueue` (the last other link ended between the container's calls),
   the container declares, binds and consumes once more; a second `UnknownQueue` refuses the attach with
   `amqp:not-found`. An attach with no source never finds a non-durable subscription: the lookup is for
   unsubscribing a durable one, and with no durable one of the name it is `amqp:not-found`.
6. **Who may.** Decision 3.7 unchanged: `sub` on the topic filter at every attach, a subscription found
   under the name judged on its own filter too, and no right on `$queue/...`. Amendment 2 governs a
   client's delete or purge of the queue by name. Its consumers count against the bus's subscription
   bound and the queue against its queue bound (`amqp:resource-limit-exceeded`).
7. **No-local** (Amendment 3, item 8) is not applied: it is left out of the echoed source and of the
   description, the queue excludes nobody and no link's own publishes are withheld. This ends Amendment 3
   item 1's treatment of a `shared` link with `durable` `none` or `link-detach` as a live subscription;
   the live topic link of its items 1 and 9 is now an unshared one only.
8. **The echo.** The container's source carries the client's address, no `durable` (so `none`),
   `expiry-policy` **`link-detach`**, no `timeout`, `distribution-mode` `copy`, the applied filters and
   the capabilities applied, `shared`, with `global` when asked. A refused attach has a null source as
   ever.
9. **Stand-in.** The queue is on the shared bus, so a message any protocol publishes is shared out the
   same way, and its changes are on the instance's event stream. It is never persisted. Reset and
   snapshot treat it as any queue that is not exclusive, so restoring a snapshot taken while links were
   attached, after they have gone, brings the queue back with no consumer: the next link under the name
   with the same description takes its messages, and it goes when that link ends.

### Alternatives considered (Amendment 4)

- **The bus's fan-out group** (`BusConnection.Subscribe(..., group:)`). A message waiting on a link
  that ends is lost, and one `released` cannot reach another link. Group names are one namespace with
  MQTT 5's shared subscriptions and NATS queue groups, so a matching name and filter would let another
  protocol's member take the messages. Two links asking different bus filters would silently form two
  groups instead of being refused. It has no advantage in link credit: neither it nor a queue consumer
  is chosen by credit.
- **The kept subscription's name.** A non-durable attach would replace, or join, a durable subscription
  of the same name, which sections 8.3.3 and 8.3.4 keep separate.
- **An exclusive queue.** Only one connection could consume it, and JMS shared consumers are commonly on
  several.
- **Deleting at a `closed` detach, as a kept subscription is.** A non-closing detach, a session's end
  and a lost connection would each need the container to delete; auto-delete covers them all.
- **The filters in the queue's name**, so differing links get separate subscriptions. Section 8.3.2
  makes that an error while a consumer is active.
- **Refusing at once on `UnknownQueue`.** A test closing one shared consumer while opening another would
  fail now and then; one more try removes that. An atomic declare, bind and consume on the bus would
  close the window completely, but is a bus operation for this one race.
- **Echoing the `expiry-policy` the client asked.** The terminus goes with its last link, so
  `link-detach` is the policy applied (decision 3.6), and Part 3 section 3.5.3 has the source say what
  is in force.
- **Leaving non-durable `shared` unserved and unechoed.** Part 3 allows it, but a JMS shared consumer
  would then receive duplicates, and root `CLAUDE.md` says nothing is left out because it is hard.

## Amendment 5 (BL-2047, 2026-11-27): who may get from or consume a subscription queue by name

Decided by Claude under Stewart's delegation.

Amendment 2 asks for `sub` on a subscription queue's filters before a delete or purge by name, but a
get or consume by name asked only for the `$queue/<name>` grant. A caller with `sub` on `$queue/#`
and none on a subscription's filter could so take its messages: AMQP 0-9-1 `basic.get`,
`basic.consume` with `no-ack`, `basic.reject` without requeue, or a STOMP `SUBSCRIBE` to
`/queue/surl-subscription-...`.

- `Surl.MessageBus`'s get and consume by name, on a queue carrying a subscription description, need
  **both** the `$queue/<name>` grant, as on any queue, **and** `sub` covering **every** filter the
  queue is bound to. Otherwise they are `AccessDenied` (AMQP 0-9-1 answers `403`), nothing is taken
  and no consumer is added. A `$queue` grant that names the queue exactly is no exception.
- A described queue bound to **no** filter is refused whenever any grant is configured, as in
  Amendment 2. With no grant, everyone may.
- The `$queue` grant is judged first, whether or not the queue exists. The filters are judged on the
  queue found, after `UnknownQueue` and `QueueLocked`, under the lock the message is taken under, and
  before any refusal that tells of the queue's consumers.
- `ConsumeSubscriptionQueue` (decision 3.7) is unchanged: `sub` on the topic filter and no `$queue`
  grant. A queue with no description is read as before. The test host's own reads (ADR-0164's browse)
  ask for no grant.
- The bindings are judged when the call is made. A consumer already added keeps receiving, and the
  binding swap Amendment 2 names also opens a get and a consume; keeping other clients' bindings off a
  described queue stays BL-2011.
- A consumer added to a queue with no description also stays when a test host's restore gives that queue
  a description, and is handed the restored messages; closing that is BL-2049.

### Alternatives considered (Amendment 5)

- **Letting an explicit `$queue/<name>` grant allow it.** A grant's filter may be a wildcard
  (`$queue/#`) written for ordinary queues, and telling an exact grant from a wildcard one is a second
  rule for a narrow gain. An account that should read a subscription is given `sub` on its filter,
  which says what it may see; secure by default.
- **Asking only for `sub` on the filters, without the `$queue` grant.** That would widen who may take a
  queue's messages by name; the subscriber already has `ConsumeSubscriptionQueue`.
- **Refusing every get and consume by name of a described queue.** A test reading a kept subscription
  from AMQP 0-9-1 or STOMP is a real use of the stand-in.
