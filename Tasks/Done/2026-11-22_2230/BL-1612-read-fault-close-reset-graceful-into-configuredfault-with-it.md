---
id: BL-1612
title: Read --fault close=reset|graceful into ConfiguredFault with its refusals
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1608, BL-1611]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-062
created: 2026-11-22
completed: 2026-11-22
---
# BL-1612 — Read --fault close=reset|graceful into ConfiguredFault with its refusals

## Goal

--fault reads close=reset|graceful into ConfiguredFault.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). Decision 4 of ADR-0188. Files: Application/Surl.Cli.UnitLibrary/ConfiguredFault.cs and ConfiguredFaultReader.cs; the key table of ADR-0124 decision 4.

## Acceptance criteria

- [x] `close` is read in any case; `close=graceful` and `close=reset` land in `ConfiguredFault`.
- [x] Refusals with these texts after `option --fault: `: `key close must be reset or graceful`, `key close does not apply to <kind>`, `key close needs key after-bytes` (for graceful without after-bytes above 0).
- [x] `close=reset` without `after-bytes` is accepted.
- [x] Tests for each refusal and each accepted spelling; coverage and complexity gates hold.

## Notes

Decided: `close` is allowed only on `drop` (via the existing key-does-not-apply refusal); `close=graceful` needs after-bytes above 0, checked after number, stage and scope refusals. Tests pass (1931).

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. close key read into ConfiguredFault with its refusals
