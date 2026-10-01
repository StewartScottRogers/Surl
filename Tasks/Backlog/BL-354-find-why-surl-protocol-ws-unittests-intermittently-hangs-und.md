---
id: BL-354
title: Find why Surl.Protocol.Ws.UnitTests intermittently hangs under the full fast-test run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-354 — Find why Surl.Protocol.Ws.UnitTests intermittently hangs under the full fast-test run

## Goal

`dotnet test --filter "TestCategory!=Integration"` never hangs in `Surl.Protocol.Ws.UnitTests`:
the test that hung is found and its race fixed or its wait bounded.

## Context

- 2026-10-01, lane 1 (BL-353): the full fast-test run on Windows hung for over an hour in
  `Surl.Protocol.Ws.UnitTests` (its testhost still alive; every other project had passed).
  Run alone straight after, it passed 184/184 in 285 ms, so the hang is intermittent and
  likely load- or order-dependent (other lanes were building and testing on the machine).
- Start with `dotnet test Surl.Protocol.Ws.UnitTests --blame-hang-timeout 2m` in a loop
  under load, and look for an unbounded await on a stream read or a `TaskCompletionSource`
  awaited without cancellation.

## Acceptance criteria

- [ ] The test that hung is named under Notes, with its cause.
- [ ] Every wait in `Surl.Protocol.Ws.UnitTests` is bounded by a cancellation token or
      timeout, so a regression fails instead of hanging.
- [ ] `dotnet test Surl.Protocol.Ws.UnitTests --blame-hang-timeout 2m` passes 20 runs in a row.

## Notes

## Log

- 2026-10-01: Created.
