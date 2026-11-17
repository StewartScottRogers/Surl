---
id: BL-1362
title: Cover the HTTP/3 GOAWAY drain branch and the datagram delivery loop end
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Http3.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1362 — Cover the HTTP/3 GOAWAY drain branch and the datagram delivery loop end

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Http3.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.9% line, 99.38% branch, 2
  failing members, in `Protocols/Http/Surl.Http3.UnitLibrary/Http3ConnectionServer.cs`:
  - `Http3ConnectionServer.EndAsync` (async) - line 394, 75% branch; line 402 is 2 of 4:
    `if (drainedGoAwayStreamId is { } goAwayStreamId)` only ever sees one outcome (a connection ended after a drain
    that sent GOAWAY, or one ended without it, is missing).
  - `Http3ConnectionServer.DeliverDatagramsAsync` (async) - line 286, 83.33% line; line 291 (after the loop, when
    `ReceiveDatagramAsync` returns `null`) is never reached: no test ends the datagram flow while delivery is running.

## Acceptance criteria

- [ ] Named `Surl.Http3.UnitTests` tests end a connection both after a graceful drain that sent GOAWAY and without one,
      asserting the frames written in each case (line 402, both outcomes).
- [ ] A named test delivers at least one datagram and then has the fake connection's `ReceiveDatagramAsync` return
      `null`, asserting delivery ends cleanly (line 291).
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Http3.UnitLibrary` reports `100 | 100` with 0 failing members, and the
      fast tests are green.

## Notes

- Tests only (test-writer). Expected bytes from RFC 9114 and RFC 9297.

## Log

- 2026-11-17: Created.
