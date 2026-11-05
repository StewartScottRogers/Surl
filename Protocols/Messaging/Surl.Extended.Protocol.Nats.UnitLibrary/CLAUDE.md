# Surl.Extended.Protocol.Nats.UnitLibrary

An extended server (ADR-0083) for the `nats` scheme the Curl port adds, which upstream curl
does not have; the composition registers it in BL-528, so `surl` does not serve it yet.
ADR-0113 decides how. It is judged by the NATS client protocol page
(<https://docs.nats.io/reference/reference-protocols/nats-protocol>, read 2026-11-05), never by
the Curl port's output, and its unit tests replay lines written by hand from that page.
`Documentation/Conformance/Extended/Nats.md` traces each requirement to its test.

It references `Surl.Protocol.Abstractions` and `Surl.MessageBus` (BL-526): `NatsServer` takes
the one `MessageBus` the process shares, never a static or a second one. `natss` comes with
BL-527.

INFO, CONNECT and keep-alive (BL-525, ADR-0113 decisions 0, 1, 2, 3, 5 and 7):

- `NatsServer` is the `IConnectionProtocolServer` for `nats`; its remarks list every answer.
  It draws its 22-character `ServerId` once, and numbers connections from 1 for `client_id`.
- `NatsServerConnection` answers one connection: INFO, then CONNECT within the head timeout
  (`-ERR 'Authorization Timeout'` without one), then PING, PONG and blank lines until the client
  closes. Its own PING goes out every `NatsServerOptions.PingInterval` (a quarter of the idle
  timeout, 120 s when it is off); at an interval with `MaxUnansweredPings` (2) unanswered it
  sends `-ERR 'Stale Connection'` and closes. However the pinging ends, reading stops with it.
- `NatsInfo` writes the INFO line: the ADR's fields in its order, `version` fixed at `2.10.0`.
- `NatsControlLineReader` reads lines ending in CR LF or a bare LF, however they are split,
  and throws `-ERR 'Maximum Control Line Exceeded'` for one over `--max-line` (ending
  included) as soon as that is certain. `NatsControlLine` upper-cases the operation.
- `NatsConnectOptions` reads CONNECT's JSON with `Utf8JsonReader`: field names match
  case-insensitively, unknown fields and `null`s are ignored, a known field of the wrong type is
  `-ERR 'Parser Error'`. `ToPasswordLogin` gives the login: `user` and `pass`, else
  `auth_token` as the empty-name account's password, else none.
- CONNECT is judged in this order: parse, then the login (an NKey or JWT, or any refusal from
  `IAuthenticationPolicy`, is `-ERR 'Authorization Violation'`), then `protocol` (0 or 1),
  then `no_responders` without `headers`. Every `-ERR` but those of a later task closes
  through `NatsProtocolViolationException`, whose message goes to the exchange log only.
- After CONNECT, an operation other than PING, PONG, PUB, HPUB (only under `headers`), SUB and
  UNSUB is `-ERR 'Unknown Protocol Operation'`, as are INFO, MSG, HMSG and a second CONNECT.

Publishing and subscribing through the shared bus (BL-526, ADR-0113 decisions 4 and 7):

- Every byte a connection sends goes through its `NatsOutbox`, in order, written by one writer.
  A bus delivery is queued there without blocking; past `NatsServerOptions.MaxPendingBytes`
  (64 MiB) unwritten the outbox overflows, the write in progress is cancelled, reading stops,
  and the server tries for `SlowConsumerErrorTimeout` (5 s) to write `-ERR 'Slow Consumer'`
  before it closes. However the writing ends, reading and pinging stop with it.
- `NatsPublishArguments`, `NatsSubscribeArguments` and `NatsUnsubscribeArguments` read the
  arguments through `NatsOperationFields`; the wrong field count or a size that is not a
  non-negative decimal integer is `-ERR 'Parser Error'`, then close. A PUB or HPUB total over
  `max_payload` (the smaller of `--max-message` and the bus's `MaxMessageBytes`) is
  `-ERR 'Maximum Payload Violation'`, then close, before the payload is read;
  `NatsControlLineReader.ReadPayloadAsync` then reads it by count and requires CR LF after it.
- Subjects map through `MessageBus.Names` for the connection's scheme. An unmappable PUB or HPUB
  subject or reply-to is `-ERR 'Invalid Publish Subject'`; an unmappable SUB subject, or one with
  `*` or `>` inside a longer token (`NatsSubjects.HasOnlyWholeTokenWildcards`), is
  `-ERR 'Invalid Subject'`; both keep the connection.
- The bus connection is made for the login's account (the empty name for a token, none when
  anonymous). A publish or subscribe `--topic-access` refuses is
  `-ERR 'Permissions Violation for Publish to <subject>'` / `... Subscription to <subject>`, a SUB
  past the bus's subscriptions per connection `-ERR 'Maximum Subscriptions Exceeded'` (both
  kept), and any other refused publish `-ERR 'Resource Limit Exceeded'`, then close.
- `NatsClientSubscription` is one SUB: its `sid`, its subject as sent, its bus subscription
  (made with `noLocal` under `echo: false`, the queue group as the bus's group) and its count
  for UNSUB's `max_msgs`. A SUB reusing a live `sid` is ignored.
- An HPUB's header block is the bus message's native form in format `nats`, byte for byte, and
  `NatsHeaderBlock.Parse` also gives its `Name: value` lines to other protocols. A delivery is
  HMSG with that block, or with `NatsHeaderBlock.Encode` of another protocol's headers, when
  CONNECT set `headers`; otherwise MSG with the payload only (`NatsMessageLine`). A topic NATS
  cannot name is not delivered, and a response topic it cannot name is left out.
- Under `no_responders`, a request no live subscription took gets `HMSG <reply-to> <sid> 16 16`
  with `NatsHeaderBlock.NoResponders` on each of the requester's own subscriptions matching the
  reply-to (`NatsSubjects.Matches`), in ordinal order of `sid`.
