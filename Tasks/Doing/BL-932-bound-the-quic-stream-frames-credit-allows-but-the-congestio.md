---
id: BL-932
title: Bound the QUIC stream frames credit allows but the congestion window holds back
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-710]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-932 — Bound the QUIC stream frames credit allows but the congestion window holds back

## Goal

A QUIC stream answer holds a bounded amount in memory even when the client grants large credit and
stops acknowledging: `QuicApplicationConnection.WriteAsync` waits on bytes queued as `STREAM` frames
but not yet sent, not only on `QuicStream.UnsentLength`.

## Context

- BL-710 (ADR-0082 amendment 3) made `WriteAsync` keep `UnsentLength` within
  `QuicApplicationConnection.MaxUnsentLength`. But `QuicStream.QueueStreamFrames` (called from
  `QuicStreams.QueueFrames`) cuts every byte the client's stream and connection credit allow into
  `STREAM` frames at once, and those frames wait in the application space for the congestion window.
  So what an answer holds is the bound plus the client's outstanding credit: a client that grants,
  say, 1 GiB of `MAX_STREAM_DATA` and never acknowledges makes the server read 1 GiB of a file into
  memory.
- Options: cut `STREAM` frames only as packets are built (in `FillPacket`, within the room left), or
  count queued-but-unsent stream bytes against the bound in `WriteAsync`'s wait.

## Acceptance criteria

- [ ] A named `Surl.Quic.UnitTests` test: with the client's credit far larger than
      `MaxUnsentLength` and no acknowledgements, a write waits once the bytes not yet sent in a
      packet pass the bound, and completes once acknowledgements open the congestion window.
- [ ] Every existing `Surl.Quic.UnitTests` test still passes; `dotnet build -warnaserror` is clean,
      the fast tests are green, and `Surl.Quic.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
