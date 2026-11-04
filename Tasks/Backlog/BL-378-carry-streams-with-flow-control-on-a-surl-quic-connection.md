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
completed:
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

- [ ] Named tests cover: out-of-order `STREAM` frames delivered in order; duplicate and overlapping
      data delivered once; data beyond the granted credit ending with `FLOW_CONTROL_ERROR`; a client
      stream beyond `MAX_STREAMS` ending with `STREAM_LIMIT_ERROR`; credit re-granted as the
      application reads; a stream's `FIN` and final size, including `FINAL_SIZE_ERROR`;
      `RESET_STREAM` and `STOP_SENDING` in both directions; and a server-initiated unidirectional
      stream opened and written.
- [ ] A test exchanges a request and a response of 1 MiB over one bidirectional stream with the
      test-side client, through flow-control updates only.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
