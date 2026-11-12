---
id: BL-932
title: Bound the QUIC stream frames credit allows but the congestion window holds back
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-710, BL-1035]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
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

- [x] A named `Surl.Quic.UnitTests` test: with the client's credit far larger than
      `MaxUnsentLength` and no acknowledgements, a write waits once the bytes not yet sent in a
      packet pass the bound, and completes once acknowledgements open the congestion window.
- [x] Every existing `Surl.Quic.UnitTests` test still passes; `dotnet build -warnaserror` is clean,
      the fast tests are green, and `Surl.Quic.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 5): Chose the second option, counting bytes cut into `STREAM` frames but not yet in a
  packet against the bound in `WriteAsync`'s wait. It is a two-line change that leaves packet building
  (`FillPacket`, splitting, retransmission order) as it is. Cutting frames only in `FillPacket` would
  change every send path, so it is not worth the risk for this bound. Code written but not built,
  because the pinned SDK 10.0.401 is missing (BL-1035); the shift stashes it:
  - `QuicServerConnection.NotYetInAPacketLength(stream)` (internal, `QuicServerConnection.Sending.cs`):
    `UnsentLength` plus the data of the stream's `STREAM` frames waiting in `application.FramesToSend`,
    including lost frames waiting to be sent again.
  - `QuicApplicationConnection.QueueWhatFitsAsync` bounds by it instead of `stream.UnsentLength`. The
    XML docs of `MaxUnsentLength` and `WriteAsync` now describe this.
  - Test `QuicApplicationConnectionTests.WriteAsync_CreditFarPastTheBoundAndNoAcknowledgements_WaitsOnceTheBytesNotYetInAPacketPassTheBoundUntilAcknowledgementsOpenTheWindow`
    (`QuicApplicationConnectionTests.Credit.cs`): both credits at 1048576000, eight `PING`s that
    acknowledge nothing, then a check that the write is still waiting; then acknowledgements until `FIN`
    and the whole body arrives.
  - Still to do once the SDK is back: build, run the test against the old line to see it fail, and
    update the `WriteAsync` paragraph in the project's `CLAUDE.md`. It still says "at most that plus the
    client's outstanding credit".

- 2026-11-12 (lane 2): The stashed code was not reachable, so rewrote it as described above: NotYetInAPacketLength, the bound in QueueWhatFitsAsync, the test (fails on the old line, passes now) and the CLAUDE.md paragraph. Built and 503 Quic tests green.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: the pinned .NET SDK 10.0.401 is not installed, so nothing builds; the fix and its test are written, not built
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. QuicApplicationConnection.WriteAsync now bounds bytes not yet in a packet
