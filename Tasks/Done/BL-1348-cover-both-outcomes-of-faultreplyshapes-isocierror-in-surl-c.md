---
id: BL-1348
title: Cover both outcomes of FaultReplyShapes.IsOciError in Surl.Cli
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1348 — Cover both outcomes of FaultReplyShapes.IsOciError in Surl.Cli

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Cli.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.94% branch, 1 failing member.
- `FaultReplyShapes.IsOciError(string)` - `Application/Surl.Cli.UnitLibrary/FaultReplyShapes.cs:103`, 50% branch (1 of 2
  at line 103): `IsCodeWithText(reply, 400, 599) && OciErrorCodes.Contains(...)` is only ever seen with one outcome.
  Which `oci` fault `--fault` replies are accepted is BL-1189's rule.

## Acceptance criteria

- [x] Named `Surl.Cli.UnitTests` tests parse an `oci` error fault rule whose reply is a 400..599 status with an OCI
      Distribution Specification v1.1.0 error code (accepted), one whose status is in range but whose text is not
      such a code (refused), and one whose status is outside 400..599 (refused).
- [x] `Measure-CodeQuality.ps1 -Library Surl.Cli.UnitLibrary` reports `100 | 100` with 0 failing members, and the fast
      tests are green.

## Notes

- Tests only. Accepted and in-range-non-code cases already existed in CommandLineParserFaultTests; added two refused DataRows (399 and 600 with a valid code) to cover the status-out-of-range branch.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. IsOciError now 100/100: added out-of-range-status refusal tests
