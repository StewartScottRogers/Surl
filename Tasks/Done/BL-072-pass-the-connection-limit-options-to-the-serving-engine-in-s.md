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
completed: 2026-09-29
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

- [x] `CommandLineRunner` builds `ConnectionLimits` from the parsed command line and passes
      it to `ServingEngine`; a `Surl.Console.UnitTests` test proves a non-default value of
      each of the four options reaches the engine.
- [x] An `--idle-timeout` or `--max-time` above `ConnectionLimits.MaxTimeout` ends surl
      with exit code 2 and no unhandled exception, pinned by a test.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Console`.

## Notes

- Plan: `CommandLineRunner.ComposeConnectionLimits` maps `SurlCommandLine`'s `MaxConnections`,
  `MaxConnectionsPerAddress`, `IdleTimeout` and `MaxTime` onto `ConnectionLimits`, and the runner
  passes it to the six-argument `ServingEngine` constructor. The tests work like the
  `ComposeContentStore` tests: each option is parsed with a non-default value and the composed
  limits are compared.
- Found: `Surl.Cli` parses `<seconds>` 0 as `Timeout.InfiniteTimeSpan` (-1 ms), which
  `ConnectionLimits` refuses as negative. The composition maps it to `TimeSpan.Zero` (no limit).
- Out of range: no new decision was needed. ADR-0007 section 2 already caps `<seconds>` at
  2147483.647 (below `ConnectionLimits.MaxTimeout`, 4294967.294 s) and refuses anything above it
  with `option <name>: expected a proper numerical parameter` and exit code 2, before the engine
  is built. `RunAsync_DurationAboveTheLongestTimeout_WritesTheRefusalAndReturnsFailedInit` pins it.
- Pipeline: the change is wiring in one method, so the plan and review stages were done in-session
  rather than by separate agents.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl's --max-connections, --max-connections-per-address, --idle-timeout and -m/--max-time now set the limits the serving engine enforces
