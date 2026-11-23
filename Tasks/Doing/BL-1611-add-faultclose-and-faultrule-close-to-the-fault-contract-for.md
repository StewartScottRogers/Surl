---
id: BL-1611
title: Add FaultClose and FaultRule.Close to the fault contract for a graceful cut
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1608, BL-1608]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1611 — Add FaultClose and FaultRule.Close to the fault contract for a graceful cut

## Goal

FaultRule carries a Close of FaultClose.Reset (default) or Graceful.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). Decision 1 of ADR-0188: the contract only; no host or engine reads it yet. Files: Foundation/Surl.Protocol.Abstractions.UnitLibrary/FaultKind.cs and FaultRule.cs.

## Acceptance criteria

- [ ] `FaultClose` enum (`Reset` = 0, `Graceful`) exists with XML docs.
- [ ] `FaultRule.Close` defaults to `FaultClose.Reset`; every existing construction compiles unchanged.
- [ ] A unit test pins the default and a `Graceful` round trip; 100% line and branch coverage kept.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
