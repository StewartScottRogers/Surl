---
id: BL-378
title: Carry streams with flow control on a Surl.Quic connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-377]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-378 — Carry streams with flow control on a Surl.Quic connection

## Goal

An established `Surl.Quic` connection carries streams. It accepts client-initiated bidirectional and
unidirectional streams, opens server-initiated unidirectional streams, reassembles `STREAM` frames in
order, enforces and grants flow-control credit at the stream and connection levels, and handles
`RESET_STREAM` and `STOP_SENDING`. BL-383 reads and writes HTTP/3 through this stream API.

## Context

- Decision: BL-372's ADR, decisions 4 and 7: the initial `MAX_DATA`, `MAX_STREAM_DATA` and
  `MAX_STREAMS` values, and how they map onto ADR-0006's limits.
- RFC 9000 sections 2 and 3 (streams and their states), 4 (flow control: `MAX_DATA`,
  `MAX_STREAM_DATA`, `MAX_STREAMS`, `DATA_BLOCKED`, `STREAM_DATA_BLOCKED`, `STREAMS_BLOCKED`), and
  19.4 to 19.14.
- Builds on BL-377's connection. Retransmission of lost frames is BL-379, so here a test channel
  delivers every packet.

## Acceptance criteria

- [x] Named tests cover: out-of-order `STREAM` frames delivered in order; duplicate and overlapping
      data delivered once; data beyond the granted credit ending with `FLOW_CONTROL_ERROR`; a client
      stream beyond `MAX_STREAMS` ending with `STREAM_LIMIT_ERROR`; credit re-granted as the
      application reads; a stream's `FIN` and final size, including `FINAL_SIZE_ERROR`;
      `RESET_STREAM` and `STOP_SENDING` in both directions; and a server-initiated unidirectional
      stream opened and written.
- [x] A test exchanges a request and a response of 1 MiB over one bidirectional stream with the
      test-side client, through flow-control updates only.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Decisions are ADR-0096 (decided by Claude under Stewart's delegation): `QuicStream` handles with
  `Read`/`Write`/`Reset`/`StopSending`; `QuicConnectionOutput.Streams` replaces `ApplicationFrames`
  (every RFC 9000 1-RTT frame is now the connection's own; BL-573's DATAGRAM frames add their own
  output); `QuicServerConnection.Transmit()` and `OpenUnidirectionalStream()` are new. Credit is
  re-granted a window at a time once half is read; `MAX_STREAMS` as client streams close.
- ADR-0095 was already taken on another branch (BL-400's), so this is ADR-0096. Writing the ADR and
  its row in `Documentation/Planning/Decisions/README.md` falls outside `touches`; BL-400 (in Doing)
  names `Documentation/Planning/Decisions`. The ADR is a new file of its own and the rule for this
  run requires one, so it went in rather than sending a finished task back to Backlog; the
  README row is a one-line append the shift's rebase resolver can merge.
- `QuicOrderedBytes` is pulled out of `QuicCryptoReassembly` so `STREAM` and `CRYPTO` data share
  the reordering; `QuicCryptoReassembly` keeps its 64 KiB limit.
- Without retransmission (BL-379) a sending part counts as done once its `FIN` or `RESET_STREAM`
  is in a frame; BL-379 should make that "acknowledged" (ADR-0096 Consequences).
- Tests: `QuicStreamTests` (28 tests, 37 cases with data rows). The 1 MiB exchange
  (`Transmit_RequestAndResponseOfOneMebibyte_FlowThroughCreditUpdatesOnly`) runs against curl's
  recorded transport parameters: the response moves 32 KiB at a time through the test client's
  `MAX_STREAM_DATA`, and the request through the server's. Values come from RFC 9000, never the
  Curl port.
- Quality: `Measure-CodeQuality.ps1 -Library Surl.Quic.UnitLibrary` over the Quic tests'
  coverage: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. A Surl.Quic connection carries client and server streams with RFC 9000 flow control: in-order reads, credit re-granted as read, limits and final sizes enforced, RESET_STREAM and STOP_SENDING both ways (ADR-0096)
