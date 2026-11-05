---
id: BL-608
title: Hold the closing and draining states on a Surl.Quic server connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-377, BL-379]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests, Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-608 — Hold the closing and draining states on a Surl.Quic server connection

## Goal

After it sends `CONNECTION_CLOSE`, a `QuicServerConnection` stays in the closing state for three
times the probe timeout and answers packets that arrive then with its `CONNECTION_CLOSE` again, at a
limited rate; after the client's `CONNECTION_CLOSE` it drains for the same time (RFC 9000 section 10.2).

## Context

- BL-377 ends a connection the moment it sends or receives `CONNECTION_CLOSE`: `IsClosed` is true and
  every later datagram is dropped, so a lost `CONNECTION_CLOSE` leaves the client to its idle
  timeout. The probe timeout comes from BL-379's loss recovery.
- Start at `SendConnectionClose` and `Discard` in `Protocols/Http/Surl.Quic.UnitLibrary/QuicServerConnection.cs`.

## Acceptance criteria

- [x] Named tests, with a fake `TimeProvider`: a packet arriving in the closing state is answered with
      the same `CONNECTION_CLOSE`; past three probe timeouts nothing is answered and `NextDeadline` is
      null; in the draining state nothing is sent.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Delivered in `QuicServerConnection.Closing.cs`: `IsClosing`, `IsDraining`,
  `ClosingPeriodProbeTimeouts` (3). Tests in `QuicServerConnectionTests.Closing.cs`:
  `ReceiveDatagram_InTheClosingState_AnswersWithTheSameConnectionClose`,
  `HandleTimeout_PastThreeProbeTimeoutsOfClosing_AnswersNothingMoreAndHasNoDeadline`,
  `ReceiveDatagram_ClientConnectionClose_DrainsSendingNothingForThreeProbeTimeouts`, and five more
  (rate, period length, before the end, the amplification limit, a repeat it holds back).
- Decisions (ADR-0082 amendment 2, added to `touches` since no task in Doing names it, to record
  them): period = 3 x (PTO + max_ack_delay) without backoff, fixed when the state starts; the
  closing state does not open what it receives and answers the 1st, 2nd, 4th, 8th... datagram with
  the exact close datagrams; draining sends nothing; `IsClosed` keeps its meaning so
  `QuicApplicationConnection` and the flow loop are unchanged.
- Found and fixed: `CONNECTION_CLOSE` was held behind a full congestion window. It is not in
  flight (RFC 9002 section 2), so it now goes in the amplification room, as `ACK` does.
- Existing tests that sent more Initials after a close now see the close repeated; they take the
  first `CONNECTION_CLOSE` instead of a single one.
- Follow-up filed: BL-843, holding a served flow open through the closing and draining states.
- Coverage of Surl.Quic.UnitLibrary: 100% line, 100% branch (cobertura from the fast tests).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. A QuicServerConnection holds the closing state (repeating CONNECTION_CLOSE at a halving rate) and the draining state for three probe timeouts
