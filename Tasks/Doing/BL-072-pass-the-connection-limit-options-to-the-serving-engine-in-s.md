---
id: BL-072
title: Pass the connection-limit options to the serving engine in Surl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-025]
touches: [Surl.Console, Surl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-072 — Pass the connection-limit options to the serving engine in Surl.Console

## Goal

`surl --max-connections`, `--max-connections-per-address`, `--idle-timeout` and
`-m`/`--max-time` change the limits the serving engine enforces, instead of the engine
always using `ConnectionLimits.Default`.

## Context

- BL-025 added `Surl.Core.ConnectionLimits` and a `ServingEngine` constructor that takes
  one. `Surl.Console/CommandLineRunner.cs` still calls the constructor without it, so the
  engine enforces ADR-0006's defaults whatever the command line says.
- `Surl.Cli`'s `SurlCommandLine` already carries `MaxConnections`,
  `MaxConnectionsPerAddress`, `IdleTimeout` and `MaxTime` (ADR-0006 section 1, ADR-0007).
- `ConnectionLimits` refuses a duration above `ConnectionLimits.MaxTimeout` (4294967294
  ms, the longest a `CancellationTokenSource` counts down). ADR-0006 says an out-of-range
  limit is `FailedInit` (2); check whether `Surl.Cli` already bounds `--idle-timeout` and
  `--max-time` there, and if not answer it with exit code 2 before the engine is built
  (decide the message by ADR-0007's conventions, and record it).

## Acceptance criteria

- [ ] `CommandLineRunner` builds `ConnectionLimits` from the parsed command line and passes
      it to `ServingEngine`; a `Surl.Console.UnitTests` test proves a non-default value of
      each of the four options reaches the engine.
- [ ] An `--idle-timeout` or `--max-time` above `ConnectionLimits.MaxTimeout` ends surl
      with exit code 2 and no unhandled exception, pinned by a test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
