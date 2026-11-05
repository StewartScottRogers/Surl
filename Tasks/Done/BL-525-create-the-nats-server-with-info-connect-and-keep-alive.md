---
id: BL-525
title: Create the NATS server with INFO, CONNECT and keep-alive
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-524, BL-404]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-525 — Create the NATS server with INFO, CONNECT and keep-alive

## Goal

`Surl.Extended.Protocol.Nats.UnitLibrary` exists. It sends INFO, reads control lines case-insensitively, checks
CONNECT's credentials and options, answers PING with PONG, and sends its own PINGs. It answers +OK under
`verbose`, and answers each protocol error with the `-ERR` the ADR names, closing where the protocol says to.

## Context

- Decision: BL-524's ADR, decisions 1, 2, 3, 5 and 7. Rules: BL-543's ADR.
- New projects in `Protocols/Messaging/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.Nats`.
  References: `Surl.Protocol.Abstractions` only. A bounded line reader may be copied from `Surl.LineProtocol`'s
  approach, but that library is the mail servers' (ADR-0050); reference it only if its ADR-0002 row allows and its
  CRLF-only lines fit (the NATS page says control lines end with CRLF).
- The protocol page: INFO, CONNECT (JSON), PING/PONG, +OK/-ERR, and its list of `-ERR` messages and which ones
  close the connection.
- JSON through `Utf8JsonReader`/`Utf8JsonWriter` (native AOT). Logins through the `Surl.Protocol.Abstractions`
  contracts. Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - INFO sent first with the ADR's fields;
      - CONNECT with good credentials accepted and `+OK` sent under `verbose`;
      - bad credentials get `-ERR 'Authorization Violation'` and are closed;
      - no CONNECT within the auth timeout closed;
      - `ping` in lower case answered `PONG`;
      - the server's PINGs sent at the interval, and the connection closed after the ADR's unanswered count;
      - a control line over the limit gets `-ERR 'Maximum Control Line Exceeded'`;
      - an unknown operation gets `-ERR 'Unknown Protocol Operation'`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Delivered: `NatsServer` (`IConnectionProtocolServer` and `IConnectionRefusalWriter`, scheme `nats`),
  `NatsServerConnection`, `NatsServerOptions`, `NatsInfo`, `NatsConnectOptions`, `NatsControlLineReader`,
  `NatsControlLine`, `NatsErrors`, `NatsProtocolViolationException`; 85 tests. Library coverage 100% line,
  100% branch (collector run 2026-11-05). The tests per criterion, all in `NatsServerConnectionTests` unless
  named: INFO - `NatsServerTests.ServeAsync_TwoConnections_SendInfoFirstWithClientIdsInAcceptOrder` and
  `NatsInfoTests.Encode_IPv4_IsTheInfoLineWithTheAdrsFieldsInOrder`; `ServeAsync_ConnectWithGoodCredentialsUnderVerbose_IsAnsweredOk`;
  `ServeAsync_RefusedLogin_IsAuthorizationViolationAndClosed`; `ServeAsync_NoConnectWithinTheHeadTimeout_IsAuthorizationTimeoutAndClosed`;
  `ServeAsync_LowerCasePing_IsAnsweredPong`; `ServeAsync_ServerPings_AreSentEveryQuarterIdleTimeoutAndTwoUnansweredCloseAsStale`;
  `ServeAsync_ControlLineOverTheLimit_IsMaximumControlLineExceededAndClosed`;
  `ServeAsync_UnknownOperationOrSecondConnect_IsUnknownProtocolOperationAndClosed`.
- Choices within ADR-0113 (sensible defaults, no new ADR needed):
  - `Schemes` is `nats` only; BL-527 adds `natss`, so INFO's `tls_required` is always `false` here.
  - `max_payload` is `--max-message` alone until BL-526 brings the bus's `MaxMessageBytes` to take the smaller of.
  - Until BL-526, PUB, HPUB, SUB and UNSUB after CONNECT are `-ERR 'Unknown Protocol Operation'` and close.
  - CONNECT is judged parse, then login, then `protocol`, then `no_responders`/`headers`, so a client that
    cannot log in learns nothing about which options the server would refuse.
  - CONNECT's field names match case-insensitively and `null` values count as absent, as the NATS server's Go
    JSON decoding does; lenient, and no client is refused for it.
  - PING or PONG with arguments is `-ERR 'Parser Error'` (decision 7, malformed arguments); blank lines are ignored.
  - The auth timeout runs until a complete CONNECT line arrives; the login check after it is outside it.
  - `--allow-anonymous` reaches the server as `NatsServerOptions.AllowsAnonymous` (INFO's `auth_required` only);
    whether a credential-less CONNECT is served stays the policy's verdict.
  - The `Line` reader is the server's own (CR LF or bare LF, per decision 0), not `Surl.LineProtocol`, which
    is the mail servers' and CRLF-only (ADR-0050).
- Also added: `WriteRefusalAsync` answers `-ERR 'Maximum Connections Exceeded'` with no INFO (matrix row
  Nats:Surl-Limits-2 maps it to this task).
- Not done here: replacing `BL-525` in `Documentation/Conformance/Extended/Nats.md` with the test names. That
  file is outside `touches` and BL-394 (in Doing) touches `Documentation/Conformance`, so it is filed as BL-781.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. NATS server sends INFO, checks CONNECT logins and options, answers PING, sends its own PINGs, and answers protocol errors with -ERR; 85 tests, 100% coverage
