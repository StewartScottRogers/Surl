---
id: BL-025
title: Enforce the decided connection limits and timeouts in Surl.Core
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-015, BL-024]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-025 — Enforce the decided connection limits and timeouts in Surl.Core

## Goal

The serving engine from BL-015 enforces every connection-level limit that the hardening
ADR (BL-024) assigns to `Surl.Core`, with the ADR's default numbers and its behaviour
when a limit is hit. Fast tests prove each limit on a hand-written `TimeProvider`.

## Context

- The hardening ADR recorded by BL-024 under `Documentation/Planning/Decisions/` is the
  specification. Its `README.md` index names it. This task covers only the rows the ADR
  assigns to `Surl.Core`: expect concurrent connections (total and per remote address),
  idle timeout and maximum exchange duration. Limits inside a protocol (head size,
  command-line length) belong to the protocol servers' own tasks.
- The serving engine and its fakes are in `Surl.Core.UnitLibrary` and
  `Surl.Core.UnitTests` (BL-015). Time comes only from the injected `TimeProvider`. No
  `Thread.Sleep`, and no package (no `FakeTimeProvider`).
- The limit values reach `Surl.Core` through the parsed command line. If BL-014 has not
  added the options yet, take them as constructor parameters with the ADR's defaults,
  and file a `Surl.Cli` task for the options.

## Acceptance criteria

- [ ] One fast test per `Surl.Core` limit in the ADR proves the default number and the
      ADR's behaviour at the limit: the connection past the limit is closed or refused
      as the ADR says, and existing exchanges are unaffected.
- [ ] Idle-timeout and maximum-duration tests advance the hand-written `TimeProvider`
      and assert the exchange is cancelled at the ADR's time and not before.
- [ ] `dotnet build Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Core.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
