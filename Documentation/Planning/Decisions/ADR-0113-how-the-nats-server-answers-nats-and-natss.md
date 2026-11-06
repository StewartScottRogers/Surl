# ADR-0113 — How the NATS server answers `nats` and `natss`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-524. BL-525 builds the server with INFO, CONNECT, logins, keep-alive, the control-line
  reader and the protocol errors (decisions 0, 1, 2, 3, 5 and 7), BL-526 routes publishes to
  subscriptions, queue groups and requests through the shared bus (decisions 3, 4 and 7), BL-734
  adds the peek decision 6 needs to the listener seam, BL-527 serves `natss` (decision 6), and
  BL-528 serves both schemes from `surl`.
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; port 4222 for both schemes),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decisions 3.2 (NATS subjects on the bus), 3.3 (queue groups are fan-out groups), 3.4 (what a
  NATS message keeps), 3.6 (the bus's bounds and their NATS answers) and 4 (a NATS login reads the
  shared accounts; a token is an empty-name account's password),
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decision 3.4 (`--topic-access` denials), [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  (secure by default), [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits and fixed
  error text) and [ADR-0010](ADR-0010-the-server-side-tls-contract.md) (TLS
  through the listener seam).
- **Amends:** nothing.

## Context

`Surl.Extended.Protocol.Nats` (in `Protocols/Messaging/`, ADR-0083's table) answers the `nats` and
`natss` schemes the Curl port adds, both on port 4222 (ADR-0083 decision 7). By ADR-0083 decision 1
it is judged by its specification, not by upstream curl, which has no NATS, and not by the Curl
port. The specifications read (2026-11-05; the pages carry no revision, so the date read is the
pin) are:

- **the NATS client protocol page**
  (<https://docs.nats.io/reference/reference-protocols/nats-protocol>; below "the protocol page"):
  "Protocol conventions", INFO, CONNECT, PUB, HPUB, SUB, UNSUB, MSG, HMSG, PING/PONG, `+OK`/`-ERR`
  and its table of `-ERR` messages with the "Recoverable" column;
- **the "Subject-Based Messaging" page** (<https://docs.nats.io/nats-concepts/subjects>; below "the
  subjects page"): tokens separated by `.`, the characters allowed, `*` and `>`;
- **the "Queue Groups" page** (<https://docs.nats.io/nats-concepts/core-nats/queue>);
- **NATS ADR-4, "NATS Message Headers"** (nats-io/nats-architecture-and-design, `adr/ADR-4.md`;
  below "the headers ADR"): the `NATS/1.0` header block, its optional status line, and the `503`
  no-responders status.

The protocol page leaves the keep-alive interval, the unanswered-PING count, the auth timeout and
the limits to the server's configuration; where it does, this ADR decides, and says so.

The port's `Curl.Extended.Protocol.Nats.UnitLibrary` and its matrix `Nats.md` were read only to
learn what a client sends, so the server accepts it: CONNECT with `verbose`, `pedantic`,
`tls_required`, `lang`, `version`, `protocol` 1 and `user`/`pass` or `auth_token`, then PUB or SUB
(with `UNSUB <sid> <n>` for a count), PING after its operations, and on `natss` a TLS handshake
from its first byte. Nothing below is taken from the port's output, and no expected byte comes from
it.

## Decision

### 0. What the server is

- **One NATS server**, core NATS only: no JetStream, no clustering, routes, gateways or leaf nodes,
  no WebSocket listener, no MQTT bridge of its own (MQTT reaches NATS through the shared bus,
  decision 4). Its subscriptions and routing are `Surl.MessageBus`'s (ADR-0100 decision 1), the one
  bus every messaging server in one `surl` shares, so a NATS subscriber receives what `mqtt`,
  `mqtt5`, `amqp` and the rest publish, and the reverse.
- **The library** references `Surl.Protocol.Abstractions` and `Surl.MessageBus` (BL-526), never
  another protocol server. JSON is read and written with `Utf8JsonReader` and `Utf8JsonWriter`
  (native AOT). Time comes from `ExchangeContext.TimeProvider`.
- **Control lines** (the protocol page, "Protocol conventions"): operation names are matched
  case-insensitively; fields are separated by one or more spaces or tabs; a line ends with CR LF,
  and a bare LF is accepted as its end too (leniency costs nothing and no client is refused for it).
  A payload is read by its byte count, CR LF inside it included, and must be followed by CR LF.
  Bytes are read however they are split across reads, and several operations may arrive before the
  server answers any; each is answered in order.

### 1. INFO (the protocol page, "INFO")

The server sends one INFO as the first line of every connection (on `natss`, decision 6 says
where), as one line of compact JSON with the fields in this order, and never sends INFO again
(no cluster changes to announce, so `protocol` 0 and 1 clients see the same):

| Field | Value | Why |
| --- | --- | --- |
| `server_id` | 22 upper-case letters and digits, random, drawn once per server start | identifies the server instance; nothing about the build |
| `server_name` | the same as `server_id` | the protocol page's default when no name is configured; no product or host name leaks |
| `version` | `"2.10.0"` | required by the page. A fixed NATS server version whose semantics this ADR follows (headers since 2.2, no-responders since 2.2), so a client library's feature detection takes the right paths; never surl's version (ADR-0006 section 3) |
| `proto` | `1` | the protocol level that supports asynchronous INFO |
| `host`, `port` | the connection's local address (no brackets for IPv6) and port | what the client connected to; nothing more |
| `headers` | `true` | HPUB and HMSG are served (decision 4) |
| `max_payload` | decision 7's bound | |
| `client_id` | the connection's number, an unsigned 64-bit integer from 1 in accept order | |
| `client_ip` | the client's address as the server sees it | only the client's own address |
| `auth_required` | `true`, or `false` under `--allow-anonymous` | decision 2 |
| `tls_required` | `true` on `natss`, `false` on `nats` | decision 6 |
| `tls_verify` | `false` | no client certificate is asked for (ADR-0010) |
| `tls_available` | `false` | `nats` never offers TLS; `natss` requires it (decision 6) |

Not sent: `go` (not a Go server), `connect_urls` and `ws_connect_urls` (no cluster and no WebSocket
listener, so there is nothing to discover), `ldm`, `git_commit`, `jetstream` (absent means none),
`ip`, `nonce` (NKeys are refused, decision 2), `cluster` and `domain`.

### 2. Authentication (the protocol page, "CONNECT"; ADR-0032; ADR-0100 decision 4)

- **Secure by default.** Unless `--allow-anonymous` is given, CONNECT must carry credentials:
  `user` and `pass` log in as that account; otherwise `auth_token` logs in as the account with an
  empty name whose password is the token (ADR-0100 decision 4). When `user` is present `auth_token`
  is ignored. Each is judged by `IAuthenticationPolicy.CheckPasswordLoginAsync` with the
  connection's encryption. `Accepted` and `AcceptedUnchecked` go on; `RefusedCredentials`,
  `RefusedAnonymous` (no credentials) and `RefusedPlaintext` all answer, after the policy's refusal
  delay (ADR-0032 section 8), `-ERR 'Authorization Violation'` and close, naming neither account nor
  cause (ADR-0032 section 5); the verbose log notes which.
- **Plain text.** The policy refuses a login on `nats` unless `--allow-plaintext-auth` is given
  (FR-028), as for every server; `natss` logs in by default.
- **Under `--allow-anonymous`** INFO says `auth_required: false`, CONNECT without credentials is
  served, and credentials given are still judged and accepted unchecked, as ADR-0049 decides for the
  mail servers; the logged-in account, or none, is the caller ADR-0102 judges.
- **NKeys and JWT are refused.** A CONNECT carrying `nkey`, `sig` or `jwt` is
  `-ERR 'Authorization Violation'`, then close, whatever else it carries: surl's accounts are a name
  and a password (ADR-0032 section 1) with no public keys or operator-signed JWTs to check them
  against, and no `nonce` is sent to sign. A later task that adds key accounts can serve them.
- **CONNECT first.** Any operation other than CONNECT before it is `-ERR 'Authorization Violation'`,
  then close, whether or not credentials are required: the protocol page has the client send
  CONNECT after INFO, and one answer for every early operation is simplest.
- **The auth timeout** is `--head-timeout` (30 s), counted from accept, the TLS handshake included:
  no complete CONNECT by then is `-ERR 'Authorization Timeout'`, then close. The protocol page names
  the error and leaves the time to the server; the slow-sender bound already covers it.

### 3. CONNECT options (the protocol page, "CONNECT")

CONNECT's argument is one JSON object. Not valid JSON, or not an object, is `-ERR 'Parser Error'`,
then close. A field of the wrong JSON type is the same. Unknown fields are ignored. A second CONNECT
on a connection is `-ERR 'Unknown Protocol Operation'`, then close (the page has one per
connection).

| Option | Default | How the server honours it |
| --- | --- | --- |
| `verbose` | `false` | when `true`, every accepted CONNECT, PUB, HPUB, SUB and UNSUB is answered `+OK`, after its effect; PING and PONG are not; an operation answered `-ERR` gets no `+OK` |
| `pedantic` | `false` | accepted and recorded; it changes nothing, because the server always checks subjects strictly (decision 4) |
| `tls_required` | `false` | ignored: on `nats` the client already learned from INFO that no TLS is offered, and on `natss` TLS is in force before CONNECT |
| `name`, `lang`, `version` | none | recorded in the verbose log only; never echoed |
| `protocol` | `0` | `0` and `1` are accepted and behave alike (no asynchronous INFO is ever sent); any other value is `-ERR 'Invalid Client Protocol'`, then close |
| `echo` | `true` | when `false`, the connection's own publishes are not delivered to its own subscriptions (and do not count as responders, decision 4) |
| `headers` | `false` | when `true`, HPUB is accepted and messages carrying headers are delivered as HMSG; when `false`, HPUB is `-ERR 'Unknown Protocol Operation'`, then close, and a message with headers is delivered as MSG with its payload only |
| `no_responders` | `false` | when `true`, a request with no subscriber gets the `503` status message (decision 4); `true` without `headers: true` is `-ERR 'No Responders Requires Headers Support'`, then close (this ADR's own text: the page lists none for it, and no-responders arrives as an HMSG) |
| `user`, `pass`, `auth_token`, `nkey`, `sig`, `jwt` | none | decision 2 |

### 4. Subjects, subscriptions, queue groups, headers and requests

- **Subjects** (the subjects page): case-sensitive tokens separated by `.`, each non-empty and free
  of whitespace. In SUB, a token that is exactly `*` matches one token, and `>` as the last token
  matches one or more. ADR-0100 decision 3.2 maps them onto the bus: `.` becomes `/`, `*` becomes
  `+`, `>` becomes `#`, and a token holding `/`, `+` or `#` is not mappable.
- **Invalid subjects.** A SUB subject with an empty token, `>` not last, `*` or `>` inside a longer
  token, or a token holding `/`, `+` or `#` is `-ERR 'Invalid Subject'`; the connection is kept
  (the page marks it recoverable). A PUB or HPUB subject or reply-to with any wildcard, an empty
  token or an unmappable token is `-ERR 'Invalid Publish Subject'` (ADR-0100 decision 3.2's text),
  its payload read and dropped, the connection kept. The check runs with or without `pedantic`.
- **PUB and HPUB** (`PUB <subject> [reply-to] <#bytes>`, `HPUB <subject> [reply-to] <#header bytes>
  <#total bytes>`) publish one message on the bus at quality of service 0 (ADR-0100 decision 3.4),
  the payload exact, the reply-to as the bus's response topic (mapped as above). A size that is not
  a non-negative decimal integer, a header size above the total, or the wrong number of fields is
  `-ERR 'Parser Error'`, then close.
- **Headers** (the headers ADR). The header block (`NATS/1.0`, an optional status code and
  description, then `Name: value` lines, then an empty line) is kept byte for byte as the message's
  native form in format `nats` (BL-706), so a NATS subscriber receives exactly the block the
  publisher wrote. Its `Name: value` lines are also parsed into the bus's string headers, names
  case-preserved, for other protocols; a block that does not parse is still delivered whole to NATS
  subscribers, and other protocols see no headers from it. A message from another protocol with
  headers reaches a NATS subscriber as HMSG with the block `NATS/1.0` CR LF, one `name: value` CR LF
  per header in order, and CR LF; with none, as MSG.
- **SUB** (`SUB <subject> [queue group] <sid>`): the `sid` is any token unique among the
  connection's live subscriptions; a SUB reusing a live `sid` is ignored (`+OK` under `verbose`).
  Each SUB is one bus subscription for the connection, checked against `--topic-access` (ADR-0102
  decision 3.4): a denial is
  `-ERR 'Permissions Violation for Subscription to <subject>'`, the connection kept, `<subject>` as
  the client sent it.
- **Queue groups** (the "Queue Groups" page): a queue group name is passed to the bus as the
  subscription's group (ADR-0100 decision 3.3), so among the group's subscriptions with the same
  subject each message goes to one, round robin. A queue group is a fan-out group, not a stored
  queue: a message published while no member is subscribed is gone. The group name is the bus's, so
  an MQTT 5 shared subscription `$share/<g>/<filter>` on the same filter is in the same group.
- **UNSUB** (`UNSUB <sid> [max_msgs]`): without `max_msgs` the subscription ends at once; with it,
  the subscription ends once it has delivered `max_msgs` messages in all, counting those delivered
  before the UNSUB, so at once when it already has. An unknown `sid` is ignored (`+OK` under
  `verbose`).
- **MSG and HMSG** carry the subject (the canonical topic mapped back: `/` becomes `.`), the `sid`,
  the reply-to when the message has a mappable response topic, and the sizes. A bus topic NATS
  cannot name (ADR-0100 decision 3.2, "What does not survive a mapping") is never delivered to
  NATS. Retained messages are never delivered (ADR-0100 decision 3.3).
- **Publishing access.** Each PUB and HPUB is checked against `--topic-access`: a denial is
  `-ERR 'Permissions Violation for Publish to <subject>'`, the connection kept, the message
  dropped.
- **Request-reply and no responders** (the headers ADR): when a PUB or HPUB with a reply-to, from a
  connection whose CONNECT set `no_responders` and `headers`, is delivered to no live subscription
  (its own subscriptions suppressed by `echo: false` do not count), the server delivers to each of
  that connection's own subscriptions matching the reply-to the message
  `HMSG <reply-to> <sid> 16 16` CR LF `NATS/1.0 503` CR LF CR LF CR LF: a 16-byte header block,
  no payload. Without `no_responders` nothing is sent and the requester times out, as the page
  says.

### 5. Keep-alive (the protocol page, "PING/PONG")

- A client's PING is answered PONG at once. A client's PONG clears the count of the server's
  unanswered PINGs.
- **The server's PING interval** is a quarter of `--idle-timeout` (30 s by default), or 120 s when
  the idle timeout is off. A quarter, not the NATS server's usual two minutes, so the stale check
  below fires before `Surl.Core`'s idle timeout closes a silent connection without a word.
- **Unanswered PINGs:** at most 2. At an interval when 2 PINGs are already unanswered, the server
  writes `-ERR 'Stale Connection'` and closes, instead of a third PING. The timer starts once
  CONNECT is accepted.

### 6. `natss` and TLS (the protocol page, INFO `tls_required`)

- **`nats`** never offers TLS: INFO says `tls_required: false` and `tls_available: false`, with or
  without a certificate, and the server never upgrades. A client wanting TLS uses `natss`.
- **`natss`** requires TLS, by either of the two forms NATS clients use, told apart by waiting
  for the client's first byte without consuming it (BL-734's peek):
  - **TLS first** (`handshake_first`, the form the port's client uses): if the client sends a byte
    within 50 ms of accept (the NATS server's fallback delay for `handshake_first: auto`), the
    server runs the TLS handshake at once and sends INFO (`tls_required: true`) inside TLS.
  - **INFO first** (the protocol page's form): otherwise the server sends INFO with
    `tls_required: true` in plain text and peeks again, up to `--head-timeout`: a first byte `0x16`
    (a TLS handshake record) starts `UpgradeToTlsAsync`; any other byte is a client writing plain
    text after being told TLS is required, answered `-ERR 'Secure Connection - TLS Required'` and
    closed.
- TLS 1.2 and 1.3 (ADR-0006 section 4), `--cert` or `--self-signed` required (`CertificateProblem`
  (58) without), no client certificate, no ALPN required. A failed handshake closes with no bytes.
  Both forms run on the one listener, port 4222, through the listener seam (ADR-0010); the server
  never constructs an `SslStream`.

### 7. Limits and errors (ADR-0006; the protocol page's `-ERR` table)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| a control line | `--max-line` (8192 bytes, line ending included; the NATS server's default is 4096) | `-ERR 'Maximum Control Line Exceeded'`, then close |
| a payload (`max_payload`) | the smaller of `--max-message` (1048576, the NATS server's default too) and the bus's `MaxMessageBytes` (`--max-filesize`); for HPUB the total size, headers included | `-ERR 'Maximum Payload Violation'`, then close, judged from the control line before the payload is read |
| an unknown operation, or INFO, MSG or HMSG from a client | — | `-ERR 'Unknown Protocol Operation'`, then close |
| malformed arguments, a payload not followed by CR LF | — | `-ERR 'Parser Error'`, then close |
| no CONNECT by `--head-timeout` | decision 2 | `-ERR 'Authorization Timeout'`, then close |
| a slow consumer | 64 MiB of messages written to the connection and not yet taken by the transport (the NATS server's default `max_pending`) | `-ERR 'Slow Consumer'` (best effort), then close; the publisher counts it as delivered |
| subscriptions | the bus's `DefaultMaxSubscriptionsPerConnection` (1000) | `-ERR 'Maximum Subscriptions Exceeded'`, the connection kept, nothing subscribed (this ADR's own text; ADR-0100 decision 3.6 says `-ERR`) |
| the bus's stores (`StoreFull`, `TooManyTopics`) | ADR-0100 decision 3.6 | `-ERR 'Resource Limit Exceeded'`, then close (this ADR's own text; the page lists none) |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s, through the server's refusal writer | `-ERR 'Maximum Connections Exceeded'`, then close, with no INFO |
| `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.Core`'s; decision 5's stale check bites first | close with no bytes |

- **Every `-ERR` text** is one of the page's, ADR-0100's `'Invalid Publish Subject'`, or this ADR's
  own three marked above, in single quotes as the page writes them. Only the client's own subject
  is ever echoed (ADR-0006 section 3); nothing names an account, a path, an exception or a version.
  "Then close" writes the error and closes gracefully (ADR-0006 section 5). The page's
  `'Attempted To Connect To Route Port'` is never sent: there is no route port.
- No new option is added: ADR-0006's options cover what a test harness tunes.

### 8. Details settled while building the routing (BL-526)

Decided by Claude under Stewart's delegation, 2026-11-05, in BL-526, where decisions 4 and 7 leave
a choice open:

- **The slow consumer's `-ERR` is bounded.** Every byte a connection sends is queued in one
  outbox, in order; past 64 MiB unwritten the write in progress is cancelled and the server tries
  for 5 seconds to write `-ERR 'Slow Consumer'`, then closes whether or not it went. A transport
  that cannot take 64 MiB will not take the error quickly either, and an unbounded try would hold
  the connection until `--max-time`.
- **The `503` goes to matching subscriptions in ordinal order of `sid`**, so the answer is the
  same every time; the headers ADR names no order.
- **Headers from another protocol** that a `NATS/1.0` block cannot carry - an empty name, a name
  holding `:`, or a CR or LF in name or value - are left out of the block, so a peer's header can
  never break the framing; with none left the message goes as MSG.
- **An HPUB with a header size of 0** carries no native form and is delivered as MSG: there is no
  block to keep.

## Alternatives considered

- **`natss` as implicit TLS only** (wrapped in `ImplicitTlsSchemeServer`, as `rediss` is).
  Rejected: the NATS protocol's own form is INFO first, then TLS, and every NATS client library
  speaks it; only TLS-first clients, the port's among them, would connect.
- **`natss` as INFO-first only.** Rejected: the port's client and any client configured with
  `handshake_first` send a ClientHello before reading INFO, and would fail.
- **Offer `tls_available` on `nats` when a certificate is configured.** Rejected: one scheme, one
  answer is simpler, and `natss` is there for TLS.
- **A two-minute PING interval** (the NATS server's default). Rejected: `--idle-timeout`'s 120 s
  would close a silent connection before a stale check ever ran.
- **Serve NKeys or JWT.** Rejected for now: surl's accounts hold no public keys or operator chains;
  a refusal with the page's own error is honest.
- **A 2 s auth timeout** (the NATS server's default). Rejected: a test harness under load can be
  slower, and `--head-timeout` is already the slow-sender bound every server uses.

## Consequences

- `surl nats://127.0.0.1:0/` (with `--allow-anonymous`, or an account) is a core NATS server on the
  shared bus: PUB, HPUB, SUB with wildcards and queue groups, UNSUB with counts, request-reply with
  no-responders, keep-alive, and `natss` over TLS in both forms.
- BL-525 to BL-527 build it, BL-734 adds the seam's peek, BL-528 serves it from `surl`, and the
  Curl port's client may connect to it as a live client in `Integration` tests (ADR-0083).
- The requirement matrix [`Nats.md`](../../Conformance/Extended/Nats.md) traces each server-side
  requirement to BL-525, BL-526 or BL-527; each task replaces its ID with the named test.
