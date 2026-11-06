# ADR-0118 — How the STOMP broker answers `stomp` and `stomps`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-530. BL-531 builds the frame codec, connection negotiation, logins and limits (decisions 0,
  1, 2, 3, 8 and the ERROR frame of decision 6), BL-532 routes messages through the shared bus with
  subscriptions, acknowledgements, receipts, transactions and heart-beats (decisions 4 to 7), and
  BL-533 serves both schemes from `surl`, `stomps` wrapped for TLS.
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; ports 61613 and 61614),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decisions 3.2 (STOMP destinations on the bus), 3.3 (named queues and live fan-out), 3.4 (what a
  STOMP message keeps), 3.6 (the bus's bounds and their STOMP answers) and 4 (a STOMP login reads
  the shared accounts),
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decisions 3.2 (declared queues) and 3.4 (`--topic-access` denials),
  [ADR-0107](ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md) (queues created on
  first use, met by STOMP on `/queue/<q>`),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) (secure by
  default), [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits and fixed error text)
  and [ADR-0010](ADR-0010-the-server-side-tls-contract.md) (TLS through the listener seam).
- **Amends:** ADR-0100 decision 3.6's STOMP cell for `TooManySubscriptions` (`ERROR` becomes `ERROR`
  then close, decision 6 below).

## Context

`Surl.Extended.Protocol.Stomp` (in `Protocols/Messaging/`, ADR-0083's table) answers the `stomp`
and `stomps` schemes the Curl port adds, on ports 61613 and 61614 (ADR-0083 decision 7). By
ADR-0083 decision 1 it is judged by its specification, not by upstream curl, which has no STOMP,
and not by the Curl port. The specification read (2026-11-05) is the **STOMP Protocol
Specification, Version 1.2** (<https://stomp.github.io/stomp-specification-1.2.html>; below "the
specification"). It has no numbered sections, so this ADR cites its section titles: "STOMP
Frames", "Value Encoding", "Repeated Header Entries", "Size Limits", "Connection Lingering",
"CONNECT or STOMP Frame", "CONNECTED Frame", "Protocol Negotiation", "Heart-beating", the client
frames "SEND", "SUBSCRIBE" (with "SUBSCRIBE id Header" and "SUBSCRIBE ack Header"),
"UNSUBSCRIBE", "ACK", "NACK", "BEGIN", "COMMIT", "ABORT" and "DISCONNECT", "Standard Headers"
("Header content-length", "Header content-type", "Header receipt"), the server frames "MESSAGE",
"RECEIPT" and "ERROR", and "Augmented BNF". Where decision 1 serves STOMP 1.0 and 1.1, the 1.1
specification (<https://stomp.github.io/stomp-specification-1.1.html>) and the 1.0 protocol page
(<https://stomp.github.io/stomp-specification-1.0.html>) are read for what differs.

The specification leaves destinations, the virtual host, the heart-beat offer, the tolerance and
the limits to the server; where it does, this ADR decides, and says so.

The port's `Curl.Extended.Protocol.Stomp.UnitLibrary` and its matrix `Stomp.md` were read only to
learn what a client sends, so the server accepts it: CONNECT with `accept-version:1.2`, `host` (the
URL's host), `heart-beat:0,0` and `login`/`passcode` from `--user`; then either SEND to the URL's
path (leading `/` kept) with `content-length` and `content-type`, followed by DISCONNECT with a
`receipt`, or SUBSCRIBE with `id` and `ack:auto`, reading MESSAGE frames until the server closes;
on `stomps`, TLS from the first byte. Nothing below is taken from the port's output, and no
expected byte comes from it.

## Decision

### 0. What the server is

- **One STOMP broker** over `Surl.MessageBus` (ADR-0100 decision 1), the one bus every messaging
  server in one `surl` shares: a message sent over `mqtt`, `mqtt5`, `amqp`, `amqp1`, `nats` or
  `kafka` on a topic reaches a STOMP subscriber of `/topic/<that topic>`, and a STOMP SEND reaches
  them. No private routing state: subscriptions, queues and redelivery are the bus's.
- **The library** references `Surl.Protocol.Abstractions` (BL-531) and `Surl.MessageBus` (BL-532),
  never another protocol server. Time comes from `ExchangeContext.TimeProvider`.
- **Frames** ("STOMP Frames", "Augmented BNF"): a command line, header lines `name:value`, an empty
  line, the body, a NUL octet. The reader accepts LF or CR LF as every EOL in every version (the
  CONNECT frame is read before a version is chosen, and leniency refuses no client), skips any
  number of EOLs between frames (heart-beats), and reads a frame however its bytes are split
  across reads; several frames may arrive before any is answered, and each is answered in order.
  Commands are matched case-sensitively, as the grammar writes them. Commands and headers must be
  valid UTF-8. The writer always ends lines with LF.
- **Bodies** ("Header content-length"): with `content-length` (a non-negative decimal integer),
  exactly that many octets are the body, NULs included, and a NUL must follow; without it, the
  body ends at the first NUL. Only SEND carries a body from a client; a body on any other client
  frame is a malformed frame (the grammar gives them none).
- **Repeated headers** ("Repeated Header Entries"): the first occurrence is used; later ones are
  ignored, and not passed on to the bus.
- **Header escapes** ("Value Encoding"): in 1.2, `\r`, `\n`, `\c` and `\\` are decoded in every
  frame but CONNECT and CONNECTED, and any other `\` sequence is a fatal malformed frame; in 1.1,
  the same without `\r`; in 1.0, nothing is escaped. The server escapes what it writes the same
  way, by the negotiated version.

### 1. Versions ("Protocol Negotiation"; "CONNECT or STOMP Frame"; "CONNECTED Frame")

- **The server speaks 1.0, 1.1 and 1.2** and chooses the highest version in the client's
  `accept-version` that it speaks. No `accept-version` header means 1.0, as the specification says.
  Serving the earlier versions costs only the differences listed below, and a complete mate refuses
  no conforming client.
- **No common version** (an `accept-version` naming none of the three) is answered, before any
  login is judged:

  ```
  ERROR
  message:Unsupported protocol version
  version:1.0,1.1,1.2
  content-type:text/plain
  content-length:43

  Supported protocol versions are 1.0 1.1 1.2^@
  ```

  then closed (decision 6), following the specification's own example for this case.
- **`STOMP` in place of `CONNECT`** is accepted and served alike, but only when the client
  accepts 1.1 or 1.2 (the STOMP command is new in 1.1); a STOMP frame negotiating 1.0 is the same
  ERROR as no common version.
- **CONNECTED** carries, in this order and unescaped: `version:<chosen>`, `heart-beat:<sx>,<sy>`
  (decision 7; omitted in 1.0), `session:<32 lower-case hex digits>` (128 random bits drawn per
  connection, naming nothing about the server or client) and `server:surl` (ADR-0006 section 3:
  the product's name, no version). No body.
- **What differs by version:**

  | Feature | 1.0 | 1.1 | 1.2 |
  | --- | --- | --- | --- |
  | Header escapes | none | `\n`, `\c`, `\\` | adds `\r` |
  | `host` | optional | as 1.2 (decision 2) | decision 2 |
  | SUBSCRIBE `id` | optional; when absent the subscription's id is its destination | required | required |
  | Ack modes | `auto`, `client` | adds `client-individual` | as 1.1 |
  | ACK and NACK name the message by | `message-id` | `message-id` and `subscription` | `id`, the MESSAGE's `ack` header |
  | NACK | an unknown command | served | served |
  | MESSAGE's `ack` header | never | never | under `client` and `client-individual` |
  | Heart-beating | none | decision 7 | decision 7 |

### 2. The `host` header and virtual hosts ("CONNECT or STOMP Frame")

**One virtual host.** Any `host` value, or none, is accepted and names the one bus; it is never
echoed, logged only under `-v`. The specification makes `host` a client duty and leaves its
meaning to the server; refusing an unknown name would only make the port's client, which sends the
URL's host, depend on how it was addressed (`localhost` or `127.0.0.1`).

### 3. Logins ("CONNECT or STOMP Frame"; ADR-0032; ADR-0100 decision 4)

- **Secure by default.** Unless `--allow-anonymous` is given, CONNECT must carry `login`; its
  `passcode` (empty when absent) is the password. Both are read unescaped, as CONNECT's headers
  are, so a colon after the first is part of the value. They are judged by
  `IAuthenticationPolicy.CheckPasswordLoginAsync` with the connection's encryption. `Accepted` and
  `AcceptedUnchecked` go on to CONNECTED; `RefusedCredentials`, `RefusedAnonymous` (no `login`)
  and `RefusedPlaintext` all answer, after the policy's refusal delay (ADR-0032 section 8), ERROR
  `message:Authentication failed` and close, naming neither account nor cause (ADR-0032 section 5);
  the verbose log notes which. A `passcode` without `login` is no credential.
- **Plain text.** The policy refuses a login on `stomp` unless `--allow-plaintext-auth` is given
  (FR-028), as for every server; `stomps` logs in by default.
- **Under `--allow-anonymous`** CONNECT without `login` is served, and a `login` given is still
  judged and accepted unchecked, as ADR-0049 decides for the mail servers; the logged-in account,
  or none, is the caller ADR-0102 decision 3.4 judges.
- **CONNECT first.** Any other frame before CONNECTED is ERROR `message:Not connected`, then
  close. A second CONNECT or STOMP is ERROR `message:Already connected`, then close.
- **The connect timeout** is `--head-timeout` (30 s) from accept, the TLS handshake included: no
  complete CONNECT by then closes with no bytes, the slow-sender bound every server uses.

### 4. Destinations ("SEND", "SUBSCRIBE", "MESSAGE"; ADR-0100 decisions 3.2 and 3.3)

The specification leaves a destination opaque. ADR-0100 decision 3.2 fixed the convention, and
this ADR applies it after the header is unescaped:

- **`/topic/<t>`** is the canonical topic `<t>` verbatim. A SUBSCRIBE may hold the bus's `+` and
  `#` levels; a SEND may not. **Live fan-out**: each message goes to every live subscription whose
  filter matches it, of any protocol. A topic keeps nothing for STOMP: a message sent while no
  STOMP subscriber is subscribed is not delivered to one later (the topic's log is Kafka's to read,
  and the retained slot MQTT's, ADR-0100 decision 3.3).
- **`/queue/<q>`** is the bus's named queue `<q>`. **Competing consumers**: each message goes to
  one of the queue's subscriptions, round robin. A queue **keeps** what nobody has consumed, up to
  the bus's bounds, and persists under `--directory`. A queue that does not exist is created by
  the first SEND or SUBSCRIBE naming it, with ADR-0102 decision 3.2's flags for a declared queue
  (durable, never exclusive, never auto-deleted), as ADR-0107 does for AMQP 1.0, so a SEND before
  any subscriber is kept and a STOMP producer meets an AMQP consumer on the same queue.
- **Anything else**, an empty `<t>` or `<q>`, or a `<t>` that is not canonical, is ERROR
  `message:Unknown destination`, then close (ADR-0100 decision 3.2's STOMP row). Unlike ADR-0107's
  AMQP 1.0 addresses, a bare name is not a queue: STOMP brokers name the kind in the destination,
  and the port's client sends the URL path whole.
- **Topic access** (ADR-0102 decision 3.4): a SEND is a publish on `<t>`, or on `$queue/<q>`; a
  SUBSCRIBE a subscribe to `<t>`'s filter or `$queue/<q>`. A denial is ERROR
  `message:Access refused`, then close, the same whether or not the destination exists.
- **SEND** ("SEND"; ADR-0100 decision 3.4) publishes one bus message at quality of service 1: the
  body exact; `content-type` as the bus's content type; every other header except STOMP's own
  (`destination`, `content-length`, `content-type`, `receipt`, `transaction`) as the bus's string
  headers, names and order kept. A SEND without `destination` is ERROR
  `message:Missing destination header`, then close.
- **SUBSCRIBE** ("SUBSCRIBE") makes one bus subscription for the connection. `id` must be unique
  among the connection's live subscriptions (ERROR `message:Duplicate subscription id`, then close);
  missing `id` in 1.1 or 1.2 or missing `destination` is ERROR `message:Missing id header` or
  `message:Missing destination header`, then close. **UNSUBSCRIBE** ("UNSUBSCRIBE") ends the
  subscription named by `id` (1.0: by `id`, or else by `destination`); its unacknowledged queue
  messages go back to the queue (decision 5). An unknown `id` is ignored, with a RECEIPT when
  asked: ending what is already ended is harmless.
- **MESSAGE** ("MESSAGE") carries, in order: `destination` (`/topic/<t>` or `/queue/<q>`, escaped
  by version), `message-id`, `subscription` (the SUBSCRIBE's `id`), `ack` (1.2 under `client` and
  `client-individual` only, equal to `message-id`), `content-type` when the bus message has one,
  `content-length` always, then the bus message's string headers in order, except any named like
  one of STOMP's own (`destination`, `message-id`, `subscription`, `ack`, `content-type`,
  `content-length`, `receipt`, `transaction`) and, in 1.0, any whose name holds `:` or whose name
  or value holds CR or LF (1.0 cannot escape them); a header holding U+0000 is never written. The
  body is the payload exact. `message-id` is a decimal counter per connection from 1, never reused
  on the connection; a redelivered message gets a new one. A topic message with no expressible
  STOMP destination is not delivered to STOMP (ADR-0100 decision 3.2).

### 5. Acknowledgement ("SUBSCRIBE ack Header", "ACK", "NACK"; ADR-0100 decisions 3.3 and 3.4)

- **`auto`** (the default): a message is acknowledged when its MESSAGE is written.
- **`client`**: an ACK acknowledges the named message and every earlier message delivered to the
  same subscription not yet acknowledged (cumulative, as the specification says). A NACK is
  cumulative the same way: the same rule for both is what clients expect of `client` mode.
- **`client-individual`** (1.1 and 1.2): an ACK or NACK settles the named message only.
- **What settles where.** For a `/queue/` subscription an ACK acknowledges the delivery to the bus
  queue, and a NACK puts the message back at its place with a redelivered mark, to go to one of the
  queue's subscriptions again (perhaps the same). For a `/topic/` subscription the bus is never
  told (ADR-0100 decision 3.4: fan-out deliveries are not acknowledged); ACK and NACK only clear
  the server's record, and a NACKed topic message is dropped, since a topic keeps nothing.
- **Redelivery** happens when a NACK says so, and for every unacknowledged queue message when its
  subscription ends by UNSUBSCRIBE, DISCONNECT or the connection's end. There is no redelivery
  limit and no dead-letter queue: the specification has neither, and the bus bounds the queue.
- **Unknown messages.** An ACK or NACK naming no outstanding message of the connection (already
  settled, never sent, or another connection's) is ignored, with a RECEIPT when asked; a late ACK
  after a cumulative one is the common case and harmless. A missing `id` (1.2) or `message-id`
  (1.0, 1.1) is ERROR `message:Missing id header`, then close. An `ack` value other than the three
  modes (or `client-individual` in 1.0) is ERROR `message:Invalid ack mode`, then close.
- No prefetch bound beyond decision 8's slow-consumer bound: the specification has none.

### 6. Transactions, receipts, DISCONNECT and ERROR

- **Transactions** ("BEGIN", "COMMIT", "ABORT"): BEGIN opens the named `transaction`, unique among
  the connection's open ones (ERROR `message:Duplicate transaction`, then close). SEND, ACK and NACK
  carrying `transaction` are held and take effect in order on COMMIT, each as if sent then;
  ABORT discards them, so held ACKs leave their messages unacknowledged. A SEND, ACK, NACK, COMMIT
  or ABORT naming no open transaction is ERROR `message:Unknown transaction`, then close; BEGIN,
  COMMIT or ABORT without `transaction` is ERROR `message:Missing transaction header`, then close.
  Open transactions are aborted when the connection ends, as the specification says. Bounds: 100
  open transactions a connection, and a transaction's held bodies at most `--max-filesize` bytes in
  all; past either, ERROR `message:Resource limit exceeded`, then close.
- **Receipts** ("Header receipt", "RECEIPT"): any client frame but CONNECT and STOMP carrying
  `receipt` is answered `RECEIPT` `receipt-id:<the value>` after the frame took effect (for SEND,
  once the bus accepted it or the transaction holds it). A `receipt` on CONNECT is ignored, as the
  specification gives CONNECT none.
- **DISCONNECT** ("DISCONNECT"): with `receipt`, the server finishes every earlier frame, writes
  the RECEIPT, and closes gracefully; without, it closes at once with no bytes. Nothing after
  DISCONNECT is read.
- **ERROR** ("ERROR"): `ERROR`, `message:<one of this ADR's fixed texts>`, `receipt-id` when the
  frame at fault carried `receipt`, and no body, except decision 1's version ERROR. Its headers are
  escaped by the negotiated version, and unescaped before negotiation. **Every ERROR is followed by
  close** ("ERROR": the server MUST close the connection after it), which amends ADR-0100 decision
  3.6's `TooManySubscriptions` cell for STOMP. The close is graceful (ADR-0006 section 5), and the
  server lingers ("Connection Lingering") up to 1 s on the `TimeProvider`, reading and discarding
  what the client still sends, so the ERROR is read before the close rather than lost to a reset.
- **Every `message` text** is one of this ADR's fixed texts: `Unsupported protocol version`,
  `Authentication failed`, `Not connected`, `Already connected`, `Unknown command`,
  `Malformed frame`, `Missing destination header`, `Missing id header`,
  `Missing transaction header`, `Unknown destination`, `Access refused`,
  `Duplicate subscription id`, `Invalid ack mode`, `Duplicate transaction`, `Unknown transaction`,
  `Invalid heart-beat`, `Header line too long`, `Too many headers`, `Frame too large`,
  `Too many subscriptions`, `Resource limit exceeded`, `Too many connections`. Only the client's
  own `receipt` value is ever echoed (ADR-0006 section 3); nothing names an account, a destination,
  a path, an exception or a version.

### 7. Heart-beating ("Heart-beating")

- **The server's offer** in CONNECTED is `heart-beat:1000,<sy>`: it can send a heart-beat as
  often as every second, and wants one from the client at least every `sy` milliseconds, a quarter
  of `--idle-timeout` (30000 by default), or 30000 when the idle timeout is off. A quarter, so a
  client heart-beating as asked never meets `Surl.Core`'s idle timeout.
- **Negotiation**, as the specification gives it for the client's `heart-beat:<cx>,<cy>` (absent
  means `0,0`): when `cy` is not 0, the server sends an EOL (LF) every `max(1000, cy)` ms in which
  it wrote nothing else; when `cx` is not 0, the server expects something from the client every
  `max(cx, sy)` ms. A `heart-beat` that is not two non-negative decimal integers separated by a
  comma is ERROR `message:Invalid heart-beat`, then close. 1.0 has no heart-beating.
- **Tolerance:** twice the expected interval. When nothing, frame or EOL, has arrived from the
  client for twice `max(cx, sy)` ms, the server closes with no bytes: a peer that stopped beating
  cannot be relied on to read an ERROR. Timers start once CONNECTED is written and run on the
  injected `TimeProvider`.

### 8. `stomps`, TLS and limits ("Size Limits"; ADR-0006; ADR-0010)

- **`stomp`** is plain text and never upgrades: STOMP has no STARTTLS.
- **`stomps`** is TLS from the first byte, `ImplicitTlsSchemeServer` around the same broker
  (ADR-0010, composed by BL-533): TLS 1.2 and 1.3 (ADR-0006 section 4), `--cert` or `--self-signed`
  required (`CertificateProblem` (58) without), no client certificate, no ALPN. A failed handshake
  closes with no bytes. The broker never constructs an `SslStream`.
- **Limits** (the specification lets a server bound header count, header line length and body
  size, and says an ERROR then close SHOULD answer a breach):

  | Limit | How the server applies it | Answer when hit |
  | --- | --- | --- |
  | a command or header line | `--max-line` (8192 bytes, EOL included) | ERROR `message:Header line too long`, then close |
  | headers in one frame | 128, a fixed bound (ADR-0006: a named constant, no option) | ERROR `message:Too many headers`, then close |
  | a body | the smaller of `--max-message` (1048576) and the bus's `MaxMessageBytes` (`--max-filesize`); judged from `content-length` before the body is read, or as it is read without one | ERROR `message:Frame too large`, then close |
  | an unknown command, or CONNECTED, MESSAGE, RECEIPT or ERROR from a client | — | ERROR `message:Unknown command`, then close |
  | a malformed frame (a header line without `:`, an undefined escape, invalid UTF-8, a bad `content-length`, no NUL after the body, a body on a frame that has none) | — | ERROR `message:Malformed frame`, then close |
  | subscriptions | the bus's `DefaultMaxSubscriptionsPerConnection` (1000) | ERROR `message:Too many subscriptions`, then close |
  | the bus's stores (`StoreFull`, `TooManyTopics`, `QueueFull`, `TooManyQueues`) | ADR-0100 decision 3.6 | ERROR `message:Resource limit exceeded`, then close |
  | a slow consumer | 64 MiB of frames written to the connection and not yet taken by the transport | ERROR `message:Resource limit exceeded` (best effort), then close; undelivered queue messages are redelivered |
  | `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s, through the server's refusal writer | ERROR `message:Too many connections`, then close, before CONNECT is read |
  | no CONNECT by `--head-timeout` | decision 3 | close with no bytes |
  | `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.Core`'s; decision 7's check bites first when heart-beating | close with no bytes |

- No new option is added: ADR-0006's options cover what a test harness tunes.

## Alternatives considered

- **Serve 1.2 only.** Rejected: 1.0 and 1.1 clients still exist, the specification's negotiation
  is built for a server speaking several, and the differences are the table in decision 1.
- **A bare destination as a queue** (as ADR-0107 decides for AMQP 1.0). Rejected: STOMP brokers
  name the kind in the destination, ADR-0100 decision 3.2 already refuses other forms, and guessing
  queue for a name meant as a topic would silently keep messages nobody reads.
- **`host` as a virtual host that must match.** Rejected: one bus, and a check that only makes
  `localhost` and `127.0.0.1` differ helps nobody.
- **Keep the connection after a recoverable ERROR** (as ADR-0100 decision 3.6 first allowed for
  `TooManySubscriptions`). Rejected: the specification has the server close after every ERROR.
- **ERROR for an ACK of an unknown message.** Rejected: a late ACK after a cumulative one is
  ordinary, and ending the session for it loses the client's other work.
- **A redelivery limit with a dead-letter queue.** Rejected: neither is in the specification, and
  the bus's bounds already stop a queue from growing without end.
- **A server heart-beat offer of 0** (no heart-beating). Rejected: a client asking for heart-beats
  to detect a dead server would get none, and the idle timeout would close silent clients without a
  word.

## Consequences

- `surl stomp://127.0.0.1:0/` (with `--allow-anonymous`, or an account and
  `--allow-plaintext-auth`) is a STOMP 1.0, 1.1 and 1.2 broker on the shared bus: `/topic/` fan-out
  and `/queue/` competing consumers shared with every messaging server, the three ack modes, NACK,
  receipts, transactions and heart-beating; `stomps` serves the same over TLS.
- BL-531 and BL-532 build it, BL-533 serves it from `surl`, and the Curl port's client may connect
  to it as a live client in `Integration` tests (ADR-0083).
- The requirement matrix [`Stomp.md`](../../Conformance/Extended/Stomp.md) traces each server-side
  requirement to BL-531 or BL-532; each task replaces its ID with the named test.
