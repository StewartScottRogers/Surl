---
id: BL-1632
title: Cover IControlTarget.WritesDeliveredTo's default implementation
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Control.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1632 — Cover IControlTarget.WritesDeliveredTo's default implementation

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Control.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Control.UnitLibrary 99.9%
  line, 100% branch, 1 failing member.
- `IControlTarget.WritesDeliveredTo` (getter), `Application/Surl.Control.UnitLibrary/IControlTarget.cs:44`, 0% line:
  `bool WritesDeliveredTo => false;` is a default interface member added by BL-1495 (commit 77071901d). Every test
  target overrides it, or none reads it through the interface without an override, so the default body never runs.
- The member is reachable: a test `IControlTarget` that does not implement `WritesDeliveredTo`, read through the
  interface (or by the endpoint seeding mail), runs it.

## Acceptance criteria

- [ ] A named `Surl.Control.UnitTests` test uses an `IControlTarget` that does not implement `WritesDeliveredTo` and
      asserts the default is false. Preferably it does this through `POST /v1/mail` seeding a message with no
      `Delivered-To:` line, so the test pins the behaviour the doc comment promises ("a target written before the option
      is unchanged").
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Control.UnitLibrary` reports `100 | 100` with 0
      failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer). No production change and no exclusion.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
