# Surl.Extended.Protocol.Nats.UnitLibrary

An extended server (ADR-0083) for the `nats` scheme the Curl port adds, which upstream curl
does not have; the composition registers it in BL-528, so `surl` does not serve it yet.
ADR-0113 decides how. It is judged by the NATS client protocol page
(<https://docs.nats.io/reference/reference-protocols/nats-protocol>, read 2026-11-05), never by
the Curl port's output, and its unit tests replay lines written by hand from that page.
`Documentation/Conformance/Extended/Nats.md` traces each requirement to its test.

It references `Surl.Protocol.Abstractions` only. Publishing and subscribing through
`Surl.MessageBus` come with BL-526, and `natss` with BL-527.

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
- Until BL-526, every operation after CONNECT but PING and PONG - PUB, HPUB, SUB and UNSUB
  included - is `-ERR 'Unknown Protocol Operation'`, as are INFO, MSG, HMSG and a second
  CONNECT.
