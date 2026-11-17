---
id: BL-1352
title: Cover the default wiring in Surl.Console Program and CommandLineRunner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1352 — Cover the default wiring in Surl.Console Program and CommandLineRunner

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Console` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.5% line, 98.89% branch, 2 failing members.
- `<RunAsync>b__2_0(ServerTlsSettings)` - `Application/Surl.Console/Program.cs:51`, 0% line: the
  `tlsSettings => CreateListenerFactory(tlsSettings, TimeProvider.System)` lambda `Program.RunAsync` hands
  `CommandLineRunner` is never called, because no fast test starts a server through `Program.RunAsync`.
- `CommandLineRunner..ctor(...)` - `Application/Surl.Console/CommandLineRunner.cs:81`, 93.75% branch; line 102 is 3 of 4:
  `configFiles ?? new DiskConfigFileSource(System.Console.OpenStandardInput, ...)`. The likely missing edge is the
  compiler's cached-delegate null check for the `System.Console.OpenStandardInput` method group, whose cached side is
  taken only when a second runner is built without `configFiles` in one process - the case BL-1055 settled by turning
  the method group into a lambda.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test invokes the listener-factory lambda `Program.RunAsync` passes (for example by
      exposing the composition as an `internal` method the test calls), and asserts it returns a listener factory; or,
      if it can run only by binding a socket, the test is marked `Integration` and the Notes say why the fast run cannot reach it.
- [ ] Line 102 of `CommandLineRunner.cs` has every branch covered: if the missing edge is the cached-delegate check, the
      method group becomes a lambda as BL-1055 did; otherwise a named test covers the missing side.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Console` reports `100 | 100` with 0 failing members, and the fast tests are green.

## Notes

- Mostly tests (test-writer); a one-line lambda change for a cached-delegate edge is allowed, as in BL-1055.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
