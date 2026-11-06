---
id: BL-454
title: Serve Socket.IO sessions over the WebSocket transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-453, BL-408]
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests, Documentation/Conformance/Extended/SocketIo.md, Documentation/Planning/Decisions/ADR-0120-how-the-socket-io-server-answers-socketio-and-socketios.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-454 — Serve Socket.IO sessions over the WebSocket transport

## Goal

The Socket.IO server accepts `GET /socket.io/?EIO=4&transport=websocket` and opens an Engine.IO session. It keeps
the session alive with pings, connects namespaces after checking `auth`, and answers events and acks as
BL-452's ADR decides. It closes on a missed pong or a DISCONNECT. HTTP long-polling is served too if the ADR
decides so.

## Context

- Decision: BL-452's ADR, decisions 1 to 7.
- The WebSocket handshake and frames: BL-408's library (add its reference). Long-polling, if served:
  BL-407's library.
- Engine.IO v4: "Handshake", "Heartbeat" (the server sends `2`, the client answers `3`; no pong within
  `pingTimeout` closes), and "Upgrade" if polling is served. Socket.IO v5: "Connection to a namespace",
  "Sending and receiving data", "Acknowledgement", "Disconnection from a namespace".
- Time through an injected `TimeProvider`.

## Acceptance criteria

- [x] Named tests, with WebSocket frames from RFC 6455 and packets from the two specifications, show:
      - the open packet sent;
      - CONNECT to `/` with good `auth` answered with the `sid` payload;
      - bad `auth` gets CONNECT_ERROR;
      - an unknown namespace gets CONNECT_ERROR;
      - an event echoed;
      - an event with an ack id answered with ACK;
      - a binary event with its attachments;
      - a ping every `pingInterval`;
      - a missed pong closing the session;
      - an over-`maxPayload` packet closing it.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-05 (BL-452): ADR-0120 is the ADR. It serves HTTP long-polling, but in BL-762, not here: this task
  answers `transport=polling` with `400` code 0 and sends `upgrades` `[]`.
- 2026-11-05 (BL-454): Built on SignalR's shape (ADR-0119): `SocketIoProtocolServer` ->
  `Http1ConnectionServer` -> `SocketIoRequestAnswerer` -> `101` -> `SocketIoWebSocketTakeover` ->
  `SocketIoSession` (read loop, clock, writer over a per-session `SocketIoOutbox`). The acceptance tests:
  open packet `SocketIoSessionTests.RunAsync_Upgrade_SendsTheOpenPacketFirst`; good auth
  `Connect_MainNamespaceWithAGoodToken_IsAnsweredWithAFreshSid`; bad auth
  `Connect_BadOrMissingAuth_IsConnectErrorAndTheSessionStaysOpen`; unknown namespace
  `Connect_UnknownNamespace_IsConnectErrorWithoutJudgingTheLogin`; echo
  `Event_WithoutAckId_IsEchoedByteForByteOnItsNamespace`; ack
  `Event_WithAnAckId_IsAnsweredWithAnAckCarryingItsArgumentsAndNotEchoed`; binary
  `Event_BinaryWithAttachment_IsEchoedWithTheSameAttachment` and
  `Event_BinaryWithAnAckId_IsAnsweredWithABinaryAckCarryingTheAttachments`; ping
  `RunAsync_Heartbeat_PingsEveryPingIntervalAfterThePong`; missed pong
  `RunAsync_NoPongWithinPingTimeout_ClosesNormally`; over-`maxPayload`
  `RunAsync_TextMessagePastMaxPayload_ClosesWithMessageTooBig`. 233 tests in the project; line and
  branch coverage 100% (`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.SocketIo.UnitLibrary`).
  Cobertura's complexity still flags three BL-453 methods (`SocketIoPacketReader.TryRead` 18,
  `TryReadAttachmentCount` 12, `SocketIoPlaceholderCheck.TakeStructure` 12, each CRAP equal to that):
  the build's CA1502 gate passes them, they predate this task, and none of this task's methods is flagged.
- Decisions (ADR-0120 amendment 1, decided by Claude under Stewart's delegation): the DISCONNECT then
  `1001` farewell is written on a limit's cancellation only, since ADR-0059 says shutdown writes nothing
  after the grace; the refusal order is transport, `sid`, method, `EIO`, missing `Upgrade: websocket`
  (code 3), then `Surl.WebSocket`'s checks, then `503`; every outgoing packet goes through one ordered
  per-session queue, an event and its attachments as one item.
- Defaults taken: the Engine.IO `sid` and each namespace `sid` come from `RandomNumberGenerator`, not
  `ExchangeContext.Identifiers` (they name a session, ADR-0124's "never seeded" list); with
  `--max-message 0` (no limit) the open packet says `"maxPayload":0`, as ADR-0120 decision 3 ties it to
  the option; a binary message with no binary packet awaiting it is a protocol error (`1002`), as
  socket.io-parser refuses it.
- Touches widened (no task in Doing names them): `Documentation/Conformance/Extended/SocketIo.md` (each
  BL-454 row now names its test, as ADR-0120's consequences require), ADR-0120 and the Decisions
  README (amendment 1).
- The test fakes (`ManualTimeProvider`, `UnitTestDuplexConnection`) are copies of SignalR's, the duplex
  connection extended with held and failing writes.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Socket.IO server serves Engine.IO v4 sessions over WebSocket: open packet, heartbeat, CONNECT with auth, echo, broadcast, acks, binary attachments and ADR-0120's limits
