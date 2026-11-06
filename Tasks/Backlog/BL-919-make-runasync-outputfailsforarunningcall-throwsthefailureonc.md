---
id: BL-919
title: Make RunAsync_OutputFailsForARunningCall_ThrowsTheFailureOnceTheSessionEnds pass every run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.McpServing.UnitTests, Protocols/Http/Surl.McpServing.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-919 — Make RunAsync_OutputFailsForARunningCall_ThrowsTheFailureOnceTheSessionEnds pass every run

## Goal

`Surl.McpServing.UnitTests`' `RunAsync_OutputFailsForARunningCall_ThrowsTheFailureOnceTheSessionEnds` passes on every run, not most.

## Context

- Seen by BL-548's lane on 2026-11-06: one fast-test run of the whole solution failed it
  (`Failed RunAsync_OutputFailsForARunningCall_ThrowsTheFailureOnceTheSessionEnds [58 ms]`), and the next
  three runs of the project alone passed it. BL-548 touched nothing in `Surl.McpServing`, so it is a race in the
  test or in `McpStreamSession`'s handling of an output failure during a running call.
- A flaky fast test blocks the dark factory's merge to `master` when it fails on CI.

## Acceptance criteria

- [ ] The race is named under Notes (what ordering makes the test fail) and fixed in the test or the library.
- [ ] `dotnet test Protocols/Http/Surl.McpServing.UnitTests --filter "Name~RunAsync_OutputFailsForARunningCall"` passes 50 runs in a row.
- [ ] `Surl.McpServing.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
