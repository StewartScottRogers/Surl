---
id: BL-607
title: Keep and retire the client's connection IDs on a Surl.Quic server connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-377]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-607 — Keep and retire the client's connection IDs on a Surl.Quic server connection

## Goal

A `QuicServerConnection` keeps the client's `NEW_CONNECTION_ID`s up to the default
`active_connection_id_limit` of 2, retires those `retire_prior_to` names with
`RETIRE_CONNECTION_ID`, and moves to a connection ID still active when the one in use is retired.

## Context

- ADR-0082 decision 4, "Connection IDs"; RFC 9000 sections 5.1.1, 5.1.2, 19.15 and 19.16.
- BL-377 takes `NEW_CONNECTION_ID` and does nothing with it (the server keeps using the client's
  first Source Connection ID), and treats every `RETIRE_CONNECTION_ID` as `PROTOCOL_VIOLATION`, which
  is right while the server issues only its handshake connection ID.
- Start at `ProcessOtherFrame` in `Protocols/Http/Surl.Quic.UnitLibrary/QuicServerConnection.Receiving.cs`.

## Acceptance criteria

- [x] Named tests: a third active connection ID is `CONNECTION_ID_LIMIT_ERROR`; a `retire_prior_to`
      covering the ID in use gets `RETIRE_CONNECTION_ID` and the server's next 1-RTT packet carries the
      remaining ID; a repeated `NEW_CONNECTION_ID` is ignored; one with a known sequence number but a
      different ID is `PROTOCOL_VIOLATION`; `NEW_CONNECTION_ID` from a client using a zero-length
      connection ID is `PROTOCOL_VIOLATION`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Delivered in the new internal `QuicPeerConnectionIds` (the client's IDs: sequence 0 is its first
  Source Connection ID), fed from `ProcessOtherFrame`; `PacketInTheClear` sends to its `InUse` ID.
  `QuicServerConnection.ActiveConnectionIdLimit` = 2, because the server sends no
  `active_connection_id_limit` (ADR-0082 decision 4 already says this; no new ADR needed).
- Named tests: `QuicServerConnectionTests.ReceiveDatagram_NewConnectionIdMakingAThirdActive_EndsWithConnectionIdLimitError`,
  `..._RetirePriorToCoveringTheIdInUse_RetiresItAndSendsToTheRemainingId`,
  `..._RepeatedNewConnectionId_IsIgnored`, `..._NewConnectionIdGivingAKnownSequenceNumberAnotherId_EndsWithProtocolViolation`,
  and `QuicPeerConnectionIdsTests.Receive_FromAClientUsingAZeroLengthConnectionId_ThrowsProtocolViolation`
  (at class level: the recorded upstream curl Initials all carry a non-empty Source Connection ID,
  and rewriting it would change the ClientHello's length, which the test client forbids).
- Choices (RFC 9000 section 19.15's MUSTs, and its MAYs taken as the stricter default): a NEW_CONNECTION_ID numbered below the
  largest Retire Prior To is retired at once; an active ID repeated under another sequence number is
  PROTOCOL_VIOLATION; the limit is checked after retirements, as section 5.1.1 says. Stateless reset
  tokens are not kept (ADR-0082: no stateless reset). The section 5.1.2 SHOULD - limiting
  unacknowledged RETIRE_CONNECTION_IDs - is not done: retirements only follow the client's own frames,
  each costing it a frame. A client's RETIRE_CONNECTION_ID stays PROTOCOL_VIOLATION, since the server
  issues only its handshake ID.
- Coverage of Surl.Quic.UnitLibrary measured with the MSTest coverage collector: 100% line, 100% branch.
  Surl.Quic.UnitTests: 471 passed.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. A Surl.Quic server connection keeps the client's NEW_CONNECTION_IDs up to 2, retires those Retire Prior To names with RETIRE_CONNECTION_ID, and sends to a still-active ID when the one in use is retired
