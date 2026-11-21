---
id: BL-1540
title: Let a command-stage delay helper report that the client left during the hold
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1532]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1540 — Let a command-stage delay helper report that the client left during the hold

## Goal

`CommandStageFaults` has a new `FindAndApplyWithClientOutcomeAsync` (connection overload) returning `CommandStageFaultResult(FaultRule? Rule, PeerWatchOutcome ClientLeft)`, where `ClientLeft` is `PeerClosed` or `PeerReset` when the hold ended because the client left and `NotWatched` otherwise; the existing overloads behave as before.

## Context

ADR-0141 Amendment 3 decisions 1 and 2. Round-trip: a `Delay` hold ended early by `PeerClosed`/`PeerReset` yields `ClientLeft` set; a completed hold, a `Drop` (the helper still aborts) and no match yield `NotWatched`. Existing `WaitForDelayOrPeerAsync` already knows the outcome. 100% line and branch coverage.

## Acceptance criteria

- [ ] The goal holds, shown by named unit tests that fail without the change.
- [ ] dotnet build is clean, fast tests pass, 100% line and branch coverage of the changed library.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
