---
id: BL-1368
title: Cover the nghttpx reply framing and HTTP/3 gateway gaps in Surl.Protocol.Http
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1368 — Cover the nghttpx reply framing and HTTP/3 gateway gaps in Surl.Protocol.Http

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.94% line, 99.86% branch, 2
  failing members, in `Protocols/Http/Surl.Protocol.Http.UnitLibrary/`:
  - `NghttpxGatewayReplyFramingReader.Read(ReadOnlySpan<byte>, bool)` - `NghttpxGatewayReplyFramingReader.cs:20`, 92.86%
    branch; line 31 is 3 of 4: `status is >= 100 and < 200` never sees one outcome (most likely a status below 100, or
    exactly 200 against 199).
  - `Http3GatewayUpstreamTestCaseServer.ServeAsync` (async) - `Http3GatewayUpstreamTestCaseServer.cs:64`, 94.12% line;
    line 89 (the method's end after `QuicDatagramFlowServer.ServeAsync` returns) is never reached: every test ends the
    flow by cancellation or exception.

## Acceptance criteria

- [ ] A named `Surl.Protocol.Http.UnitTests` test reads a reply framing whose first status is outside 100..199 on the
      side not yet covered, asserting the framing read.
- [ ] A named test serves an HTTP/3 gateway upstream test case through to a normal end of the datagram flow and asserts
      `ServeAsync` completes without exception (line 89).
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Tests only (test-writer).

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
