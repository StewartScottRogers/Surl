# ADR-0107 — How the AMQP 1.0 container answers `amqp1` and `amqp1s`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-509. BL-510 builds the type system and the frame codec (Part 1, and decision 7's frame
  limits), BL-511 the protocol headers and SASL (decisions 1 and 2), BL-512 the connection, sessions,
  links, nodes and their error conditions (decisions 3, 4 and 7), BL-513 transfers, credit,
  settlement, delivery states and the message format (decisions 5 and 6), and BL-514 the command
  line and the composition (decisions 1 and 7). BL-706 lets the bus carry the native form decision 6
  needs; BL-707 decides transactions, durable topic subscriptions and source filters (decision 5,
  "Not served yet").
- **Amended by:** the BL-513 amendment below (decisions 5 and 6) and the BL-1884 amendment below
  (decisions 2 and 7: SASL frames over 512 bytes are read).
- **Amended (2026-11-27, BL-1951):** decision 1's TLS header is also offered under TLS-SRP
  ([ADR-0122](ADR-0122-how-surl-serves-https-over-tls-srp.md) decision 5); see the note under
  decision 1's table.
- **Amended by (2026-11-27, BL-1959):**
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  (transactions, durable topic subscriptions and source filters: decision 4's `coordinator` refusal,
  decision 5's "Not served yet" bullet and a "Consequences" bullet). The same task added notes on
  decision 7's `amqp1s` bullet, pointing at
  [ADR-0122](ADR-0122-how-surl-serves-https-over-tls-srp.md) and
  [ADR-0010](ADR-0010-the-server-side-tls-contract.md), and on the `TftpTimeLimits` bullet under
  "Consequences".
- **Amended (2026-11-27, BL-1990):** notes under decision 0's second bullet, decision 3's `open`
  table, decision 4's refused-attach table and `detach` bullet, decision 5's delivery states and
  "From a topic" bullet, and the last "Alternatives considered" bullet, saying where
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  or the code differs from them.
- **Amended (2026-11-27, BL-2036):** a second note under decision 3's `open` table: a composed
  container draws its `container-id` from the `amqp1` server-life identifier source.
- **Amended (2026-11-28, BL-2044):** two notes under decision 7's table: where a sending link drops
  nothing past 1000, and that messages held in open transactions count against `--max-filesize`.
- **Amended (2026-11-28, BL-2055):** notes under decision 0's second bullet and decision 5's "From a
  topic" bullet: a non-durable shared topic subscription is a bus queue
  ([ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  Amendment 4), not a live subscription, and its links are sent queue deliveries.
- **Amended (2026-11-28, BL-2064):** a note after BL-2044's `--max-filesize` note under decision 7's
  table: under `--max-filesize 0` the messages held in open transactions have no byte bound.
- **Amended (2026-11-28, BL-2072):** a note under decision 5's "From a queue" bullet: a queue's
  messages go only to links that can send them
  ([ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  Amendment 7).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof),
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decision 4 (AMQP 1.0 logs in through `ISaslAuthenticationPolicy`),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  (nodes are the shared message bus's topics and queues; the accounts are the shared account set) and
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decisions 3.2 and 3.4 (declared queues and topic access).
- **Amends:** ADR-0100 decision 3.2's AMQP 1.0 row: the leading `/` of `/topic/` and `/queue/` is
  optional, and an address with neither prefix names the queue of that name instead of being refused
  (decision 4). `Surl.MessageBus`'s `Amqp1AddressNames` does not change: the container rewrites the
  address before calling it.

## Context

`Surl.Extended.Protocol.Amqp1` (in `Protocols/Messaging/`, ADR-0083's table) answers the `amqp1`
and `amqp1s` schemes the Curl port adds, on ports 5672 and 5671 (ADR-0083 decision 7). By ADR-0083
decision 1 it is judged by its specification, not by upstream curl, which has no AMQP, and not by
the Curl port. The specification read, and pinned, is **OASIS Advanced Message Queuing Protocol
(AMQP) Version 1.0, OASIS Standard, 29 October 2012**
(<https://docs.oasis-open.org/amqp/core/v1.0/os/amqp-core-overview-v1.0-os.html>; below "Part n"):

- Part 1, types (1.2 type system, 1.3 encodings, 1.4 composite types, 1.5 descriptors, 1.6 the
  encodings table);
- Part 2, transport (2.2 version negotiation, 2.3 framing, 2.4 connections, 2.5 sessions, 2.6 links,
  2.7 the performatives, 2.8 definitions and the error conditions of 2.8.15 to 2.8.18);
- Part 3, messaging (3.2 message format, 3.4 delivery state, 3.5 source and target);
- Part 4, transactions (BL-707);
- Part 5, security (5.1 security layers, 5.2 TLS, 5.3 SASL);

with RFC 4616 (`PLAIN`) and RFC 4505 (`ANONYMOUS`).

The port's `Curl.Extended.Protocol.Amqp1.UnitLibrary` and its matrices `Amqp1.md` and
`Amqp1-Messaging.md` were read only to learn what a client sends, so that the container accepts it:
the SASL header, `sasl-init` with `PLAIN` or `ANONYMOUS`, the AMQP header, `open` with
`max-frame-size` 65536 and no idle time-out, `begin` with windows of 2048, one `attach` whose
address is the URL's path **without its leading `/`**, URL-decoded (`amqp1://host/orders` attaches to
`orders`), then either one unsettled transfer of one `data` section after waiting for credit, or a
flow granting credit 1 and the settling `disposition` (`accepted`) of the one message it reads; empty
frames at half the container's `idle-time-out`. Nothing below is taken from the port's output, and no
expected byte comes from it.

## Decision

### 0. What the container is

- **One AMQP 1.0 container on the shared bus.** Its nodes are ADR-0100's `Surl.MessageBus` topics
  and queues, and its logins the shared account set, handed to it by the composition (BL-514), so a
  message sent over `amqp1` reaches an `mqtt` subscriber and the reverse. The library references
  `Surl.Protocol.Abstractions` and `Surl.MessageBus`, never another protocol server.
- **State of its own** is per connection: the SASL state, the sessions with their windows and
  handles, the links with their credit, unsettled deliveries and partial transfers. Nothing of a
  connection outlives it; a link is never resumed (decision 5).

  *Amended 2026-11-27 (BL-1990), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  decisions 3.1 and 3.3 to 3.6:* a kept topic subscription is a bus queue, so something a link
  made can outlive the link, and even its connection. A client receiver's topic source keeps one,
  shared or not, when its `durable` is above `none` and its `expiry-policy` is not `link-detach`
  (an absent or unknown policy is read as `session-end`).

  - With `never` it outlives its link, session and connection, until a `detach` with `closed` true
    destroys it or a later attach replaces it, and under `--directory` it is persisted with the bus.
  - With `session-end` or `connection-close` it is a queue exclusive to the connection, so it never
    outlives the connection. It outlives a link detached with `closed` false until its session ends
    (`session-end`) or its connection ends (`connection-close`).
  - A `shared` source with `durable` `none` or `link-detach` makes a live subscription, which ends
    with its link.

  A link is still never resumed (ADR-0146 decision 3.1).

  *Amended 2026-11-28 (BL-2055), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  Amendment 4:* that amendment replaced the last bullet of the BL-1990 note above, and the code
  follows it. A client receiver's topic source whose `capabilities` include `shared` and that asks
  no kept subscription (`durable` `none` or absent, whatever its `expiry-policy`, or any `durable`
  with `expiry-policy` `link-detach`) makes a non-durable shared subscription, not a live one. It is
  a bus queue named `surl-subscription-nondurable-` and 64 hex digits, auto-delete only (not
  durable, not exclusive, never persisted), bound to the topic filter, and every link on the
  subscription competes to consume it. It outlives a link, and that link's connection, while another
  link consumes it, and the bus removes it, with its messages, when its last consumer ends. A live
  subscription is now an unshared topic source's only.

- The container acts only as the peer that **accepts** connections: it never opens one, never
  begins a session or attaches a link of its own accord, and answers each of the client's.

### 1. Protocol headers (Part 2 section 2.2; Part 5 sections 5.1, 5.2 and 5.3.1)

The header is the 8 bytes `A M Q P`, a protocol id, then major 1, minor 0, revision 0.

| Client's first header | `--allow-anonymous` off | `--allow-anonymous` on |
| --- | --- | --- |
| `AMQP 3 1 0 0` (SASL) | answered `AMQP 3 1 0 0`, then `sasl-mechanisms` (decision 2) | the same |
| `AMQP 0 1 0 0` (AMQP, no SASL) | answered `AMQP 3 1 0 0`, then closed: a login is required, and Part 2 section 2.2 answers a header the server will not accept with the one it would, then closes | answered `AMQP 0 1 0 0`; the connection is anonymous |
| `AMQP 2 1 0 0` (TLS) on `amqp1` with a certificate (`--cert` or `--self-signed`) | answered `AMQP 2 1 0 0`, then the TLS handshake (`IConnection.UpgradeToTlsAsync`, TLS 1.2 and 1.3); inside it the client starts again with a SASL or AMQP header, judged by this table | the same |
| `AMQP 2 1 0 0` on `amqp1` without a certificate, or on `amqp1s` (already TLS), or after a TLS layer | answered `AMQP 3 1 0 0`, then closed | the same |
| any other 8 bytes (another version, AMQP 0-9-1's `AMQP 0 0 9 1`, not `AMQP` at all) | answered `AMQP 3 1 0 0`, then closed | the same |

*Amended 2026-11-27 (BL-1951), pointing at
[ADR-0122](ADR-0122-how-surl-serves-https-over-tls-srp.md) decision 5:* TLS-SRP, as well as a
certificate, makes the TLS upgrade available. The third row also covers `amqp1` under TLS-SRP, and
the fourth row's "on `amqp1` without a certificate" means with neither a certificate nor TLS-SRP.
`ServerTlsComposition.IsTlsUpgradeAvailable` is true when `--cert` or `--self-signed` is given or
TLS-SRP is served, and `Surl.Composition` passes it to the container as
`Amqp1SecurityNegotiationOptions.CanUpgradeToTls`. Under TLS-SRP, with a certificate or without one,
the handshake after the header is TLS-SRP's (ADR-0122 decision 5), which is TLS 1.2 only (its
decision 2), not the third row's "TLS 1.2 and 1.3". ADR-0122 Amendment 4 accepts the TLS-SRP options
when `amqp1://` is the only listen URL.

- **After a successful SASL outcome** the client sends `AMQP 0 1 0 0` and the container answers it
  (Part 5 section 5.3.1); any other header there is answered `AMQP 0 1 0 0` and closed.
- "Closed" means the container writes its header and ends its outgoing stream with no frame, as
  Part 2 section 2.2 says; it does not wait to read more.
- **Pipelining** (Part 2 section 2.4.1, Part 5 section 5.3.1): a client may send a header and the
  frames after it without waiting; the container reads them in order and never drops bytes that
  arrived early, except bytes read beyond a TLS header, which are discarded before the handshake as
  `IConnection.UpgradeToTlsAsync` requires.
- The header answer is written once the client's header is read: the container never speaks first,
  so the client's header decides which layer comes next.

### 2. SASL (Part 5 section 5.3; RFC 4616; RFC 4505)

- **Frames.** SASL frames are type `0x01`, channel ignored, at most **65536 bytes**, or
  `--max-message` when smaller, never below 512 (the BL-1884 amendment below, which replaces the
  512 bytes first decided here); a larger frame, an AMQP frame, or a SASL frame out of order closes
  the connection with no bytes: the SASL layer has no error frame.
- **`sasl-mechanisms`** lists the mechanisms `ISaslAuthenticationPolicy.GetSaslMechanisms` returns for
  the connection's scheme and TLS session, in the policy's order, followed by **`ANONYMOUS` under
  `--allow-anonymous` only**. The policy withholds `PLAIN` and the other plain-text mechanisms on a
  connection without TLS unless `--allow-plaintext-auth` (FR-028) is given, so `amqp1://` offers
  only the challenge mechanisms by default and `amqp1s://` (or `amqp1` after a TLS header) offers
  `PLAIN` too. A list that would be empty is still sent as an empty array; every login then fails.
  The field is multiple: one mechanism is written as a single symbol, several as an array of
  symbols.
- **`sasl-init`** names a mechanism and carries its initial response; `hostname` is read and
  ignored. A mechanism not offered is answered **`sasl-outcome` code 1 (`auth`)**. `ANONYMOUS`
  (allowed) is accepted at once; its trace is not checked. Every other mechanism runs through
  `ISaslAuthenticationPolicy.StartSaslExchange`: each challenge is a `sasl-challenge`, each answer
  a `sasl-response`, until the policy decides.
- **`sasl-outcome`**:

  | Code | Sent when |
  | --- | --- |
  | 0 `ok` | the policy accepts the login, or `ANONYMOUS` under `--allow-anonymous`; `additional-data` only when the mechanism's last server message needs it (SCRAM's server-final) |
  | 1 `auth` | the policy refuses the login, the mechanism was not offered, or `PLAIN`'s `authzid` is neither empty nor the `authcid` (RFC 4616 section 2: Surl grants no proxy rights) |
  | 2 `sys`, 3 `sys-perm`, 4 `sys-temp` | never: the container has no backing system to fail; a fault inside the policy is a server fault, closing the connection with no bytes and a note |

  A refusal is written after the policy's one-second delay (ADR-0032 section 8), never names the
  account or the cause (ADR-0032 section 5), and the connection is then closed: Part 5 offers no
  retry on one connection.
- **Under `--allow-anonymous`** a client that still logs in with an offered mechanism runs its steps
  and is accepted unchecked, as ADR-0049 decides for the mail servers' SASL. The account logged in,
  or none, is the caller ADR-0102's topic access judges (decision 4).

### 3. `open`, and idle time-out (Part 2 sections 2.4 and 2.7.1)

- **The container's `open`** answers the client's, after reading it (never before, since the
  client's header decides the layer):

  | Field | Value | Why |
  | --- | --- | --- |
  | `container-id` | `surl-` and 32 lower-case hex digits from `RandomNumberGenerator`, fixed for the server's life | unique, as Part 2 section 2.7.1 requires, and naming no host |
  | `hostname` | absent | only the connecting peer sends it |
  | `max-frame-size` | **65536**, or `--max-message` when that is smaller (never below 512) | what the port's client and most AMQP 1.0 containers offer; a frame is read whole before it is decoded, so the frame is the read bound ADR-0006's `--max-message` sets |
  | `channel-max` | **63** (64 sessions) | decision 7's session bound |
  | `idle-time-out` | **60000** milliseconds | half of `--idle-timeout` (120 s), as Part 2 section 2.4.5 advises a peer to advertise; with another `--idle-timeout` it is half of that, and absent when the idle timeout is off |
  | `outgoing-locales`, `incoming-locales` | absent (`en-US` implied) | error descriptions are English |
  | `offered-capabilities`, `desired-capabilities` | absent | none is served |
  | `properties` | `product` = `surl` | no version is sent (ADR-0006 section 3) |

  *Amended 2026-11-27 (BL-1990):* the `container-id` row's source of digits differs in the code.
  `Amqp1Container` draws them once, when it is created, from the `IIdentifierRandom` it is given,
  and only when its options give no `container-id` (`Amqp1ContainerOptions.NewContainerId`).
  `RandomNumberGenerator` is only the unseeded fallback it uses when given none,
  `Amqp1UnseededIdentifierRandom`. `Surl.Composition` gives it none, so a container it composes
  draws from that fallback even when the run is seeded.

  *Amended 2026-11-27 (BL-2036):* `Surl.Composition` now gives the container it composes an identifier
  source, so the last sentence of the BL-1990 note above no longer describes the code. A composed
  container draws its `container-id` from the `amqp1` server-life source (ADR-0124 decision 6,
  `ComposedProtocolServices.IdentifiersForServer("amqp1")`): with `--random-seed`, runs with the same
  seed send the same `container-id`; without it the source is cryptographic.

- **The client's `open`**: `hostname` is read and ignored (one container, no virtual hosts); its
  `max-frame-size` bounds every frame the container writes, which it splits transfers to fit
  (decision 6); a `max-frame-size` below 512 closes the connection with **`amqp:invalid-field`**
  (Part 2 section 2.7.1, MIN-MAX-FRAME-SIZE); its `channel-max` bounds the channel numbers the
  container uses.
- **The container's own idle time-out.** When no frame (an empty frame counts) arrives for
  `--idle-timeout`, the container writes `close` with **`amqp:resource-limit-exceeded`** and the
  description `idle time-out expired` and ends the connection (Part 2 section 2.4.5: close with an
  error explaining why).
- **The client's idle time-out.** When the client's `open` carries a nonzero `idle-time-out`, the
  container writes an empty AMQP frame (8 bytes, no body) whenever it has written nothing for half
  that time, at most once every 100 milliseconds. Both timers run on `ExchangeContext.TimeProvider`,
  never `Thread.Sleep` or polling.
- **`close`** from the client is answered with `close` (no error) and the connection ends; open
  sessions and links end with it, their consumers returning unsettled queue messages to the bus.
  A `close` carrying an error is answered the same and the error noted in the exchange log.
- **A frame before `open`** other than `open`, or a second `open`, closes the connection with
  **`amqp:connection:framing-error`**. A frame whose size is below 8, whose `DOFF` is below 2 or
  points past the frame, or whose body is not a performative the container knows, closes it with
  **`amqp:connection:framing-error`**; a performative that decodes but whose mandatory field is
  missing or of the wrong type closes it with **`amqp:decode-error`**.

### 4. Sessions, links and nodes (Part 2 sections 2.5, 2.6.3 to 2.6.6, 2.7.2, 2.7.3, 2.7.7, 2.7.8; Part 3 section 3.5)

- **Sessions.** A `begin` without `remote-channel` starts a session; the container answers on the
  same channel number when it is free, otherwise the lowest free one, never above either peer's
  `channel-max`, with `remote-channel` the client's channel, `next-outgoing-id` 0,
  `incoming-window` and `outgoing-window` **2147483647**, and `handle-max` **255**. A `begin` on a
  channel above the container's `channel-max`, or on a channel already in use, closes the
  connection with **`amqp:connection:framing-error`**. A frame other than `begin` on a channel with
  no session closes the connection with **`amqp:connection:framing-error`**. `end` is answered with
  `end`; its links are detached with it.
- **Session windows** (Part 2 section 2.5.6): the container never writes more transfers than the
  client's `incoming-window` allows, waiting for the client's `flow` when it reaches 0. Its own
  windows are effectively unbounded because memory is bounded per message and per link instead
  (decision 7); a client exceeding them is impossible.
- **Links.** An `attach` with a handle at or below `handle-max` and not in use attaches a link; a
  handle in use ends the session with **`amqp:session:handle-in-use`**, and a handle above
  `handle-max` ends it with **`amqp:session:handle-in-use`** too (Part 2 section 2.7.3 forbids it;
  the specification names no other condition for it). A `flow`, `transfer`, `disposition` or
  `detach` naming a handle not attached ends the session with **`amqp:session:unattached-handle`**
  (Part 2 section 2.7.5 and the others).
- **The container's answering `attach`**: the role opposite the client's; the same `name` and a
  handle of its own; for a client receiver (container sending) a `source` that echoes the client's
  address and states `distribution-mode` `move` for a queue and `copy` for a topic, and
  `initial-delivery-count` 0; for a client sender (container receiving) a `target` that echoes the
  address, `rcv-settle-mode` `first`, and `max-message-size` the bus's `MaxMessageBytes`
  (`--max-filesize`). `snd-settle-mode` echoes the client's when the client is the sender and is
  the client's request when the container is the sender (decision 5). `unsettled` is absent and
  `incomplete-unsettled` false.
- **Addresses** (Part 3 sections 3.5.3 and 3.5.4 leave them opaque; ADR-0100 decision 3.2 maps
  them). The container takes the source address of a client receiver or the target address of a
  client sender and:
  1. drops one leading `/`;
  2. `topic/<t>` is the canonical topic (or, for a source, filter) `<t>`;
  3. `queue/<q>` is the queue `<q>`;
  4. **any other non-empty address `<a>` is the queue `<a>`** - so `amqp1://host/orders`, the form
     the port's client and most AMQP 1.0 clients use, sends to and receives from queue `orders`;
  5. it then passes `/topic/<t>` or `/queue/<q>` to `Amqp1AddressNames`.

  This amends ADR-0100 decision 3.2's AMQP 1.0 row, which refused an address without a prefix:
  a bare name as a queue is what AMQP 1.0 clients expect of a broker, and a STOMP destination still
  needs its prefix, so the two meet on `/queue/<q>` as before. A `to` property and a link address the
  container writes always carry the full `/topic/` or `/queue/` form.
- **Nodes are created on first attach.** A queue that does not exist is declared on the bus by the
  first attach naming it, as a sender or a receiver, with ADR-0102 decision 3.2's flags for a
  declared queue: **durable**, never exclusive, never auto-deleted. So a receiver attached before
  anything was sent waits on the queue, and a send before any receiver is kept. A topic needs no
  creation (ADR-0100 decision 3.3). Creating queues on demand is the anonymous-safe default because
  the bus bounds them (decision 7).
- **Dynamic nodes** (Part 3 section 3.5.3, `dynamic` true on a client receiver's source, as a
  request-and-reply client asks for its reply address): the container declares a queue named
  `surl-dynamic-` and 32 hex digits, **exclusive to the connection and auto-deleted** when the link
  detaches, not durable, and answers with that address as `/queue/<name>` in the source. A
  `dynamic` target (a client sender) is answered the same way.
- **A refused attach** (Part 2 section 2.6.3): the container answers with an `attach` whose source
  (client receiver) or target (client sender) is null, then at once a `detach` with `closed` true
  and the error:

  | Cause | Error condition |
  | --- | --- |
  | no address and not `dynamic`; an address that maps to no canonical topic or filter or queue name (`InvalidName`); a wildcard (`+`, `#`) in a client sender's target | **`amqp:not-found`** |
  | ADR-0102 decision 3.4's topic access denies the caller: `sub` for a client receiver, `pub` for a client sender, `$queue/<q>` for queues | **`amqp:unauthorized-access`** (ADR-0102's AMQP 1.0 row) |
  | the bus refuses the queue (`TooManyQueues`) or the subscription (`TooManySubscriptions`) | **`amqp:resource-limit-exceeded`** (ADR-0100 decision 3.6) |
  | a `coordinator` target (Part 4, BL-707) | **`amqp:not-implemented`** |

  The description is fixed text from the container's table (`no such node`, `not authorized`,
  `resource limit exceeded`, `transactions are not supported`), never the address. Access is denied
  the same way whether or not the node exists (ADR-0006 section 3).

  *Amended 2026-11-27 (BL-1959), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  decision 1:* the table's last row, the refusal of a `coordinator` target, is replaced, and the
  replacement is built (BL-956). The container answers a `coordinator` target as a receiving link and
  serves transactions on it, so it no longer writes the description `transactions are not supported`.
  A `coordinator` as a source is refused with `amqp:not-allowed`.

  *Amended 2026-11-27 (BL-1990), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  decisions 2.2 and 3.3 to 3.5:* these attaches are refused too, with the fixed description given.

  - A client receiver's source carries a selector filter that is not a string or that the selector
    parser refuses, its length and nesting bounds included: **`amqp:invalid-field`**,
    `selector not valid`.
  - A client receiver's attach with a source asks for a kept subscription that is held: a link
    already consumes it and this attach is not shared or asks something else, or its queue is
    exclusive to another connection: **`amqp:resource-locked`**, `subscription in use`.
  - A source or target address names a `surl-subscription-` queue, as
    `/queue/surl-subscription-...` or as the bare name: **`amqp:not-found`**, `no such node`.
  - A client receiver's attach with no source has a link name that finds no kept subscription (one
    in a queue exclusive to another connection is not found): **`amqp:not-found`**,
    `no such node`.
- **`detach`** from the client is answered with `detach` carrying the same `closed`; a link
  suspended (`closed` false) is ended all the same, since nothing of a link is kept for resumption.
  Either way a receiving client link's unsettled queue deliveries go back to the queue, marked
  redelivered.

  *Amended 2026-11-27 (BL-1990), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  decisions 3.1 and 3.3 to 3.6:* "nothing of a link is kept for resumption" does not cover a kept
  subscription (the note under decision 0). A `detach` with `closed` false leaves its queue: a
  `never` subscription then outlasts the session and the connection too, a `session-end` or
  `connection-close` one only until that event. While it lasts, a later attach that names it again,
  asking the same address, filters, expiry policy and sharing, consumes what the queue holds. A
  `detach` with `closed` true destroys it, deleting the queue and its messages, unless another link
  still consumes it (a shared subscription goes with its last link). The link itself is still never
  resumed: its unsettled deliveries go back to the queue as this bullet says.

### 5. Credit, settlement and delivery states (Part 2 sections 2.6.7, 2.6.12, 2.7.4, 2.7.6, 2.8.2, 2.8.3; Part 3 section 3.4)

**The container as receiver** (a client sender):

- **Credit.** Right after its `attach` the container sends a `flow` granting **link credit 100**;
  whenever the remaining credit falls to 50 it sends a `flow` raising it back to 100. A client
  `flow` with `echo` true is answered with the container's `flow`; one with `drain` true changes
  nothing (the container always has credit to give).
- **A transfer beyond credit** (the client's delivery count past the credit given) detaches the link
  with **`amqp:link:transfer-limit-exceeded`** (Part 2 section 2.8.18).
- **Settlement.** The container's `rcv-settle-mode` is `first`, whatever the client asked: it
  settles each unsettled delivery as soon as it decides, with a `disposition` (role receiver,
  `settled` true, the state). A delivery the client sent pre-settled (`snd-settle-mode` `settled`,
  or `settled` true on the transfer) gets no `disposition`, as Part 2 section 2.6.12 says.
- **The states it uses: `accepted` and `rejected` only.**

  | Outcome of storing the message | State |
  | --- | --- |
  | stored on the bus: a topic's fan-out and log, or a queue (the publish is the bus's, ADR-0100 decision 3.3) | **`accepted`** |
  | topic access denies the publish to this topic (a later grant change; ADR-0102 decision 3.4) | **`rejected`**, error `amqp:unauthorized-access` |
  | the bus refuses it: `StoreFull`, `TooManyTopics`, `QueueFull`, `TooManyQueues` | **`rejected`**, error `amqp:resource-limit-exceeded` |
  | its sections do not decode as Part 3 section 3.2's message, or its `message-format` is not 0 | **`rejected`**, error `amqp:decode-error` (or `amqp:not-implemented` for a `message-format` other than 0) |

  `released` and `modified` say the receiver did not process the message, which the container never
  does: it stores or refuses. A pre-settled message that would have been rejected is dropped and the
  reason noted in the exchange log. `received` is never sent: the container does not resume
  deliveries.

  *Amended 2026-11-27 (BL-1990), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  decisions 1.3 to 1.5 and 1.8:* `accepted` and `rejected` are no longer the only states the
  container writes. It also writes:

  - `declared`, carrying the new `txn-id`, as the state of the `disposition` answering a `declare`
    on a coordinator link (decision 1.3);
  - `transactional-state` with outcome `accepted` or `rejected`, answering a transfer posted in an
    open transaction (decision 1.4); a `txn-id` naming no open transaction is answered with a plain
    `rejected` and `amqp:transaction:unknown-id`;
  - `transactional-state` with no outcome, on its own transfers on a link whose last `flow` carried
    the `txn-id` of a transaction still open, which acquires them in it (decision 1.5).

  Neither a `declare` the client sent settled (decision 1.8) nor a posting it sent settled gets an
  answer.

- **Quality of service on the bus** (ADR-0100 decision 3.4): an unsettled delivery is published at
  QoS 1, a pre-settled one at QoS 0.

**The container as sender** (a client receiver):

- **Credit.** The container sends as many deliveries as the client's `link-credit` allows, in queue
  or publish order, advancing its `delivery-count` per Part 2 section 2.6.7. A `flow` with `drain`
  true makes it send what it holds up to the credit, then advance `delivery-count` by the credit
  left, set credit 0 and answer with a `flow` (`drain` true); `echo` true is answered with a `flow`.
- **From a queue** the link is one bus consumer (`BusConnection.Consume`) with `maxUnacknowledged`
  1000 (decision 7); deliveries the bus hands it while it has no credit wait on the link, in order,
  and go back to the queue when the link ends.

  *Amended 2026-11-28 (BL-2072), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  Amendment 7:* no delivery waits on a queue link. The link's consumer has the link's credit as its
  delivery credit, or 0 while its session's window is closed or the link holds 1000 unsettled, and a
  delivery is claimed from the bus only as the container starts writing it; the rest stay ready in the
  queue. This replaces this bullet's last clause, what the BL-1990 note under "From a topic" says of
  deliveries "still waiting for credit" and of filters and unnameable topics applying as a message
  leaves the queue (they now apply as it is claimed), and what the BL-2055 note says of "waiting"
  deliveries going back marked redelivered: what was never sent is not marked.
- **From a topic** the link is one live bus subscription to the filter; messages published while it
  has no credit wait on the link, at most 1000 (decision 7), and a message arriving past that is
  dropped for this link and noted: fan-out is at most once (ADR-0100 decision 3.4).

  *Amended 2026-11-27 (BL-1990), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  decisions 1.5, 3.1, 3.2, 3.5 and 3.6:* this bullet, and "For a topic delivery every outcome only
  settles it" below, hold for a live subscription only. A link on a kept subscription (the note
  under decision 0), durable or shorter-lived, shared or not, consumes the subscription's bus
  queue, bound to the filter, and what it is sent are queue deliveries. (An attach with no source
  only looks the subscription up, and consumes nothing.)

  - **Waiting bound.** Nothing is dropped at the link past 1000. Its consumer holds at most 1000
    unacknowledged (`MaxWaitingDeliveries`), those still waiting for credit and those kept back as
    `undeliverable-here` included, and the bus keeps the rest in the queue. The queue has the bus's
    queue bounds (ADR-0100 decision 3.6): past them the copy of a publish into it is refused, while
    the publish itself still succeeds.
  - **Filters** run as a message leaves the queue: one they refuse, or one on a topic the client
    cannot name, is acknowledged and not sent.
  - **Outcomes** act on the queue as "The client's outcome" below says of a queue delivery:
    `accepted` acknowledges, `rejected` drops, `released` and `modified` put the message back, and
    `modified` with `undeliverable-here` keeps it from this link until the link ends. A delivery
    sent settled is acknowledged as it is written, and one unsettled when the link ends goes back
    to the queue. A transaction's commit applies a held outcome the same way, and its rollback
    returns to the queue the deliveries acquired in it and not settled outside it: ADR-0146
    decision 1.5's "Topic deliveries have nothing to return" does not hold for them.

  *Amended 2026-11-28 (BL-2055), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
  Amendment 4:* what the BL-1990 note above says of a link on a kept subscription also holds for a
  link on a non-durable shared one (the BL-2055 note under decision 0), which is therefore not a
  live subscription. The link consumes the subscription's bus queue and is sent queue deliveries,
  with the same waiting bound, filters and outcomes, transactions included, and "For a topic
  delivery every outcome only settles it" does not hold for it. What its links leave unsettled or
  waiting goes back to the queue, marked redelivered, for the links still consuming it; when the
  last one ends, the queue goes with its messages. The note's parenthesis on an attach with no
  source is about kept subscriptions only: such an attach never finds a non-durable shared one.

- **Settlement follows the client's `snd-settle-mode` request:** `settled` - every delivery is sent
  pre-settled and, from a queue, acknowledged to the bus as it is written; `unsettled` or `mixed` -
  every delivery is sent unsettled and settled by the client's `disposition`. The client's
  `rcv-settle-mode` is honoured: under `first` the client's settling `disposition` ends the
  delivery; under `second` the container answers the client's unsettled `disposition` with its own
  settling one, and the delivery ends when the client then settles.
- **The client's outcome** of a queue delivery: `accepted` acknowledges it (`BusQueueConsumer.Acknowledge`);
  `rejected` drops it (`Reject` without requeue; the bus keeps no dead-letter queue); `released`
  puts it back for any consumer (`Reject` with requeue); `modified` puts it back the same way,
  marked redelivered, unless `undeliverable-here` is true, in which case the link keeps it
  unacknowledged and out of its own sending until the link ends, when it goes back to the queue for
  other consumers - so it is never redelivered to the modifying link, as Part 3 section 3.4.5
  requires. A `received` state is ignored. For a topic delivery every outcome only settles it:
  fan-out has nothing to put back.
- **Not served yet: transactions** (Part 4: a `coordinator` target, `declare`, `discharge`,
  `transactional-state`), **durable topic subscriptions** (a source on `/topic/` with `durable`
  above `none` and `expiry-policy` `never`) and **source filters** (the `filter` map). BL-707
  decides them. Until then a `coordinator` target is refused (decision 4); a topic source asking
  durability is answered with `durable` `none` in the container's source, and a `filter` map is left
  out of the container's source, which Part 3 section 3.5.3 defines as the filters not applied - the
  client decides whether to go on. A link is never resumed: a client's `unsettled` map is ignored
  and each delivery is new (Part 2 section 2.6.13).

  *Amended 2026-11-27 (BL-1959), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md):*
  that ADR replaces this "Not served yet" bullet, and what replaces it is built: transactions
  (decision 1; BL-956 and BL-957), source filters (decision 2; BL-958 and BL-959) and durable and
  shared topic subscriptions (decision 3; BL-960). The bullet's refusal and interim answers no longer
  hold. Its last sentence still does: a link is never resumed (ADR-0146 decision 3.1).

### 6. Transfers, the message, and storage (Part 2 sections 2.6.12, 2.6.14, 2.7.5; Part 3 section 3.2)

- **Multi-frame transfers.** A delivery spans transfers while `more` is true; the container joins
  them in order. A continuation whose `delivery-id` or `delivery-tag` is present and differs from the
  first's, or that arrives on another link's handle while a delivery is open, detaches the link with
  **`amqp:invalid-field`**. A transfer with `aborted` true discards the delivery: nothing is stored,
  nothing settled (Part 2 section 2.7.5). The container writes its own deliveries the same way,
  every frame at most the client's `max-frame-size`, `more` on all but the last.
- **The largest message** is the bus's `MaxMessageBytes`, `--max-filesize` (100 MiB by default),
  counted over the transfer payload as it is joined. Past it the link is detached with
  **`amqp:link:message-size-exceeded`** (ADR-0100 decision 3.6) without reading more of the
  delivery into memory. The partial deliveries of one connection together are bounded by the same
  number; past that the connection is closed with **`amqp:resource-limit-exceeded`**.
- **The message on the bus** (ADR-0100 decision 3.4):
  - **payload**: the `data` sections' bytes, concatenated in order; or an `amqp-value` that is a
    `binary` (its bytes) or a `string` (its UTF-8, with `Utf8Payload` set); or, for any other
    `amqp-value` or for `amqp-sequence` sections, the sections' own AMQP encoding as written;
  - **expiry**: the earlier of the header's `ttl` from the publish time and the properties'
    `absolute-expiry-time`;
  - **content type**: the properties' `content-type`;
  - **response topic**: the properties' `reply-to`, when it maps to a topic or queue by decision 4's
    rules, otherwise dropped; **correlation**: the properties' `correlation-id`, its bytes for a
    `binary`, its UTF-8 for a `string`, its 16 bytes for a `uuid`, its big-endian 8 bytes for a
    `ulong`;
  - **headers**: each `application-properties` entry whose value is a `string`, in order;
  - **retain** false; QoS as decision 5.
- **The native form.** Beside those fields the container stores, through BL-706's
  `BusMessage.NativeForm` in format `amqp1`, the message as it arrived **from the
  `message-annotations` section to its end** (message annotations, the bare message and the footer),
  byte for byte, with the header's `durable`, `priority`, `ttl`, `first-acquirer` and
  `delivery-count`. Delivery annotations are not kept: Part 3 section 3.2.2 addresses them to the
  next hop only.
- **What an `amqp1` receiver gets:**
  - a message with an `amqp1` native form: a header rebuilt from the stored fields, with
    `delivery-count` raised by one and `first-acquirer` false when the bus marks the delivery
    redelivered (Part 3 section 3.2.1), then the stored bytes unchanged - so the bare message is
    exactly what the sender wrote, as Part 3 section 3.2 makes it immutable;
  - any other message: a `header` only when it has a `ttl` to carry (the expiry's remaining
    milliseconds) or a redelivery to count; `properties` with `to` the full address, `creation-time`
    the publish time, and `content-type`, `reply-to` (as `/topic/` or `/queue/`) and
    `correlation-id` (`binary`) when the bus message has them; `application-properties` from the
    headers, the first of a repeated name kept (an AMQP map has unique keys); and one `data` section
    holding the payload.
- **Storage across restarts is the bus's** (ADR-0100 decision 3.7): queues, which decision 4 makes
  durable, and topic logs persist under `--directory <path>` in `<path>/.surl/bus`, the native form
  with them (BL-706); without `--directory` they live in memory. The container writes no file of its
  own. A message is accepted once the bus holds it; a failed persistence write is noted, never
  answered as an error (ADR-0100 decision 3.6's `StorageFailed` row).

### 7. `amqp1s`, and the ADR-0006 limits

- **`amqp1s`** is the container behind implicit TLS from the first byte (Part 5 section 5.2.1's
  alternative establishment, the IANA `amqps` port 5671), wrapped in `ImplicitTlsSchemeServer` by
  BL-514 as `mqtts` is (ADR-0010): TLS 1.2 and 1.3 (ADR-0006 section 4), `--cert` or
  `--self-signed` required (`CertificateProblem` (58) without), no client certificate asked for, no
  ALPN agreed (AMQP 1.0 registers none). Inside it the headers are decision 1's, a TLS header being
  answered `AMQP 3 1 0 0` and closed. `amqp1` reaches the same container in clear text, or behind
  TLS after a TLS header (decision 1).

  *Amended 2026-11-27 (BL-1959), pointing at
  [ADR-0122](ADR-0122-how-surl-serves-https-over-tls-srp.md) decisions 2, 3 and 6 and Amendment 2,
  and [ADR-0010](ADR-0010-the-server-side-tls-contract.md) section 5:*

  - Under TLS-SRP `amqp1s` needs neither `--cert` nor `--self-signed`:
    `ServerTlsComposition.FindListenUrlWithoutCertificate` does not report it. The handshake is
    TLS 1.2 only (ADR-0122 decision 2). The listener's ALPN list is not consulted (Amendment 2,
    "ALPN"): `Surl.TlsSrp` agrees `http/1.1` when the client offers it, refuses an ALPN list without
    it with the alert `no_application_protocol` (120) (decision 3), and agrees none when the client
    offers none. The same handshake follows `amqp1`'s TLS header. With a certificate and without
    TLS-SRP, no ALPN is agreed, as the bullet says.
  - "No client certificate asked for" holds only without `--cacert`. With it, every TLS handshake on
    every listener requires a client certificate (ADR-0010 section 5), `amqp1s`'s and the one after
    `amqp1`'s TLS header included. `--cacert` is refused beside TLS-SRP (ADR-0122 decision 6).

  *Amended 2026-11-27 (BL-1959), pointing at
  [ADR-0010](ADR-0010-the-server-side-tls-contract.md) section 2 and Amendment 1:* `amqp1s` is not
  wrapped in `ImplicitTlsSchemeServer`, and neither is `mqtts`. `Surl.Composition` registers one
  `Amqp1Container` for both `amqp1` and `amqp1s`. `TlsSchemes` lists `amqp1s`, so
  `ListenUrl.IsImplicitTls` is true for it unless set otherwise, and `Surl.Core`'s `ServingEngine`
  runs the implicit TLS handshake before the container sees the connection.

| ADR-0006 limit or bound | How the container applies it | Answer when hit |
| --- | --- | --- |
| `--max-message` (1 MiB) | the `max-frame-size` the container offers is the smaller of 65536 and it; a frame's size field above the offered size (512 for `open` itself; in SASL the offered size, per the BL-1884 amendment) | `close` with `amqp:connection:framing-error` after `open`; before it, no bytes |
| `--max-filesize` (100 MiB) | one message, as joined; all partial deliveries of a connection | detach with `amqp:link:message-size-exceeded`; close with `amqp:resource-limit-exceeded` |
| `--head-timeout` (30 s) | from accept to the client's `open` read, the headers, a TLS handshake and SASL included | close with no bytes |
| `--idle-timeout` (120 s) | no frame for that long (decision 3) | `close` with `amqp:resource-limit-exceeded` |
| `--max-time` (3600 s), `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s | `close` with `amqp:resource-limit-exceeded` when `open` has been exchanged, otherwise no bytes |
| sessions a connection: 64 (`channel-max` 63) | decision 4 | `close` with `amqp:connection:framing-error` |
| links a session: 256 (`handle-max` 255) | decision 4 | `end` with `amqp:session:handle-in-use` |
| link credit given: 100 a link | decision 5 | detach with `amqp:link:transfer-limit-exceeded` |
| deliveries waiting on a sending link: 1000 (queue `maxUnacknowledged`; topic buffer) | decision 5 | none: the bus holds the rest (queue), or the message is dropped for that link (topic) |
| the bus's bounds (`StoreFull`, `TooManyTopics`, `QueueFull`, `TooManyQueues`, `TooManySubscriptions`) | ADR-0100 decision 3.6's AMQP 1.0 column | `rejected` or a refused attach with `amqp:resource-limit-exceeded` |

*Amended 2026-11-28 (BL-2044), pointing at
[ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
decisions 3.1 to 3.6 and Amendment 4:* in the "deliveries waiting on a sending link" row, "topic
buffer" and "the message is dropped for that link (topic)" hold for a live subscription only. A
topic link on a subscription's bus queue, a kept subscription's (the note under decision 0) or a
non-durable shared one's, consumes that queue with `maxUnacknowledged` 1000
(`MaxWaitingDeliveries`), and nothing is dropped at the link: the bus holds the rest in the queue.
The queue has the bus's queue bounds (ADR-0100 decision 3.6): past them a topic publish's copy into
it is refused, while the publish itself still succeeds. A non-durable shared subscription has been
such a queue since Amendment 4 was built, so the last bullet of the BL-1990 note under decision 0,
which calls it a live subscription, no longer describes the code.

*Amended 2026-11-28 (BL-2044), pointing at
[ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
decision 1.4:* the `--max-filesize` row names one message and the partial deliveries only. Messages
held in open transactions count against `--max-filesize` too, in a check of their own
(`Amqp1ContainerConnection`'s `Hold`).

- A posting whose bytes, added to the bytes held in all the connection's open transactions and the
  bytes of its partial deliveries, come to more than the limit is not held. It is answered with a
  `disposition` whose state is `transactional-state` with outcome `rejected` and the error
  `amqp:resource-limit-exceeded`. This closes neither the link nor the connection, and the
  transaction stays open. A posting the client sent settled gets no answer.
- The partial-delivery check does not count held bytes. It compares the connection's partial bytes
  alone with the bus's `MaxMessageBytes`, which `Surl.Composition`'s `SurlServerComposition` sets
  from `--max-filesize` (no bound when it is 0), and closes the connection past it, as the row says.
- The held-bytes check compares with the exchange's `MaxUploadBytes`, which is the option's number,
  and, unlike ADR-0006, does not read 0 as no limit: under `--max-filesize 0` every posting of one
  byte or more in an open transaction that would otherwise be held is refused as the first bullet
  says.

*Amended 2026-11-28 (BL-2064), pointing at
[ADR-0006](ADR-0006-hardening-for-internet-facing-use.md):* BL-2063 changed `Hold`, so the third
bullet of the BL-2044 note above no longer describes the code. The held-bytes check now reads a
`MaxUploadBytes` of 0 as no limit, as ADR-0006 does: under `--max-filesize 0` it applies no bound,
so no posting is refused for its size and the messages held in a connection's open transactions have
no byte bound. What is still capped is how many transactions are open at once
(`MaxOpenTransactions`, 100). The first bullet's refusal applies only under a limit above 0.

- No new option is added: ADR-0006's options and the bus's bounds cover every store a peer can fill;
  64, 256, 100 and 1000 are named constants of the library.
- Every `description` the container writes is fixed text from its own table, never naming a path,
  an address, an account or an exception (ADR-0006 section 3); `info` maps are never sent.

## Alternatives considered

- **Only `/topic/` and `/queue/` addresses, as ADR-0100 first decided.** Rejected: an AMQP 1.0 client
  addresses a broker's queue by its bare name, and the port's client sends `amqp1://host/orders` as
  `orders`; refusing it would make the commonest use fail with `amqp:not-found`. STOMP keeps its
  prefixes, and both still meet on `/queue/`.
- **Only queues declared with `--queue`.** Rejected: a test server whose first send is refused
  because nobody declared the queue is a worse default than one bounded by the bus's `MaxQueues`,
  and it would make `amqp1` differ from AMQP 0-9-1's `queue.declare`.
- **Plain AMQP accepted without SASL by default.** Rejected by ADR-0032: logins are required unless
  `--allow-anonymous` is given.
- **Refusing the TLS header and offering TLS only on `amqp1s`.** Rejected: Part 5 section 5.2 defines
  the in-band upgrade, the listener seam already offers it (`IConnection.UpgradeToTlsAsync`, as the
  mail servers' `STARTTLS` uses), and root `CLAUDE.md` leaves nothing out because it is extra work.
- **`released` for a message the bus refuses.** Rejected: `released` invites the sender to try again
  elsewhere at once, while a full store stays full; `rejected` with `amqp:resource-limit-exceeded`
  says why.
- **Keeping only the protocol-neutral fields.** Rejected: an `amqp1` receiver would lose the
  sender's `message-id`, typed application properties and body section types, which Part 3 section
  3.2 forbids an intermediary to change; BL-706 adds the native form instead.
- **Serving transactions, durable subscriptions and filters now.** Deferred to BL-707, which decides
  them: each needs bus support (a transactional publish, a subscription without a connection,
  selector evaluation) that ADR-0100 does not have, and the decision is better made on its own.

  *Amended 2026-11-27 (BL-1990), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md):*
  the deferral is over. BL-707 decided all three in that ADR on 2026-11-06, and they are built. The
  bus gained a batch publish, a consumer predicate and a subscription description on a named queue
  (its decisions 1.6, 2.4 and 3.2); selectors are evaluated by the AMQP 1.0 library, which the bus
  knows nothing of (its "Alternatives considered").

## Consequences

- `surl amqp1://127.0.0.1:0/` (with `--allow-anonymous`, or an account over TLS or with
  `--allow-plaintext-auth`) is an AMQP 1.0 container over the shared bus: any address is a queue
  created on first attach, `/topic/<t>` a bus topic, with credit, settlement and multi-frame
  transfers, and a message keeps its AMQP sections from `amqp1` sender to `amqp1` receiver.
- `Surl.MessageBus` gains the native form (BL-706); BL-513 depends on it.
- Transactions, durable topic subscriptions and source filters wait for BL-707.

  *Amended 2026-11-27 (BL-1959), pointing at
  [ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md):*
  they no longer wait. ADR-0146, decided in BL-707, replaces this bullet, and all three are built
  (BL-956 to BL-960).

- The requirement matrix [`Amqp1.md`](../../Conformance/Extended/Amqp1.md) traces each server-side
  requirement of Parts 1, 2, 3 and 5 to BL-510 to BL-513; each task replaces its ID with the named
  test.
- `TftpTimeLimits` does the same for `tftp://` (BL-350): `-m` passes at `MaxTime` after
  `OperationStarted`, and the timeout message's N counts from there. The connect timeout
  and the re-send schedule still count from the handler call. Measured with
  `curl -sS -L -m 2 --proto-redir =tftp` and a first hop that answers 302 to a silent
  `tftp://` port after 1.5 seconds: exit 28,
  `Operation timed out after 2011 milliseconds with 0 bytes received` (BL-350 Notes).

  *Amended 2026-11-27 (BL-1959):* the `TftpTimeLimits` bullet above is no part of this decision,
  which is about AMQP 1.0, and it describes no Surl type: no code in this repository is named
  `TftpTimeLimits`. Commit `08b010bb5` (BL-513) added it. Read nothing about `tftp://` from it.

## Amendment: how BL-513 reads decisions 5 and 6 (2026-11-05)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-513,
where decisions 5 and 6 leave a choice open. Each follows the specification and the simplest faithful
reading; none adds an option.

- **Credit to a client sender.** The container counts the sender's `delivery-count` against the limit
  its credit gave. A client `flow` on such a link sets the `delivery-count` it states (the sender owns
  it, Part 2 section 2.6.7) and changes nothing else; only a transfer that leaves 50 or less raises the
  credit back to 100. So a sender whose `flow` claims a `delivery-count` at or past the limit has no
  credit, and its next transfer detaches the link with `amqp:link:transfer-limit-exceeded`; an echoed
  `flow` then states credit 0, never a negative count wrapped round.
- **Transfers the container cannot place.** A transfer on a link the client receives on detaches that
  link with `amqp:illegal-state`. A first transfer without a `delivery-id`, which Part 2 section 2.7.5
  makes mandatory, is decision 6's continuation with no delivery open: `amqp:invalid-field`. After the
  container detaches a link, the client's transfers and flows on it are ignored until its `detach`.
- **Frames the container writes** are at most the smaller of the client's `max-frame-size` and the
  container's own, so a transfer never exceeds what either side offered. A delivery is written frame by
  frame while the client's session `incoming-window` allows and finished as later `flow`s open it, so a
  message larger than the window never stalls its link.
- **Unsettled deliveries on a sending link** count toward decision 7's 1000 as waiting ones do: a link
  holding 1000 the client has not settled sends nothing more until it settles one, whatever its credit,
  so a topic receiver that never settles cannot grow the container's memory.
- **Settlement by a client receiver.** A `disposition` settling a delivery without an outcome (no
  `state`, or `received`) puts a queue delivery back as `released` does: the client did not process it
  (Part 3 section 3.4). In `rcv-settle-mode` `second` the container settles a delivery once the client
  states an outcome and forgets it then; the client's own settling `disposition` that follows finds
  nothing and changes nothing. An outcome is applied to the bus once, whatever later dispositions say.
- **Refused consumes.** A link the bus will not let consume - `QueueLocked` (a queue exclusive to
  another connection) or `ExclusiveConsumeRefused` (another consumer holds it exclusively) - is refused
  at attach with `amqp:unauthorized-access`, as topic access is: the same answer whether or not the
  queue exists. Queue access is asked of the bus as the topic `$queue/<q>`
  (`MessageBus.GetTopicAccess`); for a queue name holding `/` that splits the name into levels where
  the bus's own queue check keeps it one, so a grant naming such a queue exactly may admit an attach
  whose send or consume the bus then refuses (`rejected`, or the attach refused).
- **A `reply-to` naming a queue** has no bus field: the bus's response topic is a canonical topic, which
  a queue is not. It travels in the native form, so an `amqp1` receiver still gets it, and other
  protocols do not see it. Decision 6's "or queue" is narrowed to topics for that reason.
- **An `absolute-expiry-time` beyond what a `DateTimeOffset` holds** (before year 1 or after 9999) sets
  no expiry rather than ending the connection.
- **A correlation-id of any other type** than `binary`, `string`, `uuid` or `ulong` gives no bus
  correlation data.

## Amendment: SASL frames over 512 bytes are read (BL-1884, 2026-11-27)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-27, in
BL-1884. Replaces the 512-byte SASL limit of decision 2 and of decision 7's `--max-message` row.

### Context

- Part 5 section 5.3.1 says "The maximum size of a SASL frame is defined by MIN-MAX-FRAME-SIZE" and
  "There is no mechanism within the SASL negotiation to negotiate a different size."
  MIN-MAX-FRAME-SIZE is 512.
- The SASL `GSSAPI` AP-REQs [ADR-0057](ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md)
  amendment 1 measured are 635 bytes or more. Under the 512-byte limit a `sasl-init` carrying one
  closed the connection before the ticket was checked, although the AMQP servers offer `GSSAPI`
  (ADR-0057 amendment 4).
- Widely used AMQP 1.0 peers read more than 512 bytes in SASL (their sources, read 2026-11-27; none
  was run):

  | Source | What it does |
  | --- | --- |
  | Apache Qpid Proton C, `478e670` | `c/src/core/dispatcher.c`, `pn_dispatcher_sasl_input` reads a SASL frame up to `transport->local_max_frame`, which `transport.c` starts at `PN_DEFAULT_MAX_FRAME_SIZE`, 32 KiB (`engine-internal.h`) |
  | Apache Qpid ProtonJ2, `a6cef0f` | `ProtonEngineSaslDriver.DEFAULT_MAX_SASL_FRAME_SIZE` is 4096, settable, never below 512 |
  | RabbitMQ, `c3c4ac5` | `deps/rabbit/include/rabbit_amqp_reader.hrl`: `INITIAL_MAX_FRAME_SIZE` 8192, "Allow for potentially large sets of tokens during the SASL exchange" |
  | OASIS AMQP Claims-based Security 1.0, CSD01 (a committee draft), section 4.2.1 | an implementation of its mechanism "MUST support a maximum SASL frame size of 8192" |

- The Curl port was not read for this (ADR-0083).

### Decision

- **The largest SASL frame the container reads is the `max-frame-size` it would offer in its
  `open`:** 65536 bytes, or `--max-message` when that is smaller, never below 512; `--max-message 0`
  (no limit) leaves 65536 (`Amqp1FrameLimits.LargestSaslFrameSizeRead`). It covers `sasl-init` and
  `sasl-response`, on `amqp1` and `amqp1s`. 65536 is at least what each peer above reads by
  default, so a frame one of them accepts by default surl accepts too.
- A larger SASL frame is refused from its header, its body unread, and closes the connection with no
  bytes, as before.
- **No option, and no warning.** This relaxes no login rule: every mechanism is offered and judged
  as before, and `GSSAPI` is offered only when the run asked for it (`--auth gssapi` with
  `--keytab`). What grows is what one connection that has not logged in can make surl hold, from
  512 bytes to at most 65536, still inside `--max-message`, `--head-timeout` and the connection
  limits. A limit that made the requested `GSSAPI` login fail until a second option was given would
  protect nothing.
- **The exchange log still withholds the password.** `Amqp1SaslInitBytesRedactor` (ADR-0182) follows
  SASL frames up to 65536 bytes, so a `PLAIN` password in a frame over 512 bytes is masked; at the
  old bound it would have been logged as received.
- Unchanged: the client's `open` is still read up to 512 bytes (Part 2 section 2.4.1), and the SASL
  frames the container writes are as before.

### Alternatives considered

- **Keeping 512, as the specification says.** Rejected: no Kerberos login could succeed, and a
  stand-in broker should take the logins the brokers it stands in for take.
- **8192** (RabbitMQ, the claims-based security draft) **or 32 KiB** (Proton C). Rejected: each is
  another constant that refuses tickets some peer above would read; a Kerberos ticket grows with the
  authorisation data a directory puts in it. The container already reads 65536-byte frames after
  `open`, so one bound serves both.
- **A larger limit only while `GSSAPI` is offered.** Rejected: two limits to test and explain, for
  no protection beyond what `--max-message` already gives.

### Consequences

- An AMQP 1.0 client's `GSSAPI` ticket reaches `GssapiSaslExchange`.
- surl accepts a SASL frame the specification forbids a client to send. No conforming client sees a
  difference: its frames are 512 bytes at most.
- A `--max-message` smaller than the `sasl-init` frame still keeps such a ticket out, as it
  bounds the frames surl reads.
