---
id: BL-105
title: Write the exchange log at a chosen log level, with one info line per connection and --trace-time stamps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-101]
touches: [Surl.Output.UnitLibrary, Surl.Output.UnitTests]
requirement: FR-013
created: 2026-09-29
completed:
---
# BL-105 — Write the exchange log at a chosen log level, with one info line per connection and --trace-time stamps

## Goal

`Surl.Output`'s exchange log factory writes what ADR-0033 says each level writes - nothing,
errors only, one info line per connection, or today's verbose log - and, with
`--trace-time`, stamps each line with the time from an injected `TimeProvider`.

## Context

FR-013; ADR-0033 (BL-101) decisions 1, 3, 5 and 7 give each level's output, the info line's
exact text and trigger, the timestamp format and which lines get it. Trace dumps are
BL-106; wiring into `surl` is BL-107.

- `Surl.Output.UnitLibrary/VerboseExchangeLogFactory.cs`: `VerboseExchangeLogFactory(TextWriter
  writer, bool verbose)` returns `VerboseExchangeLog` or `SilentExchangeLog.Instance` from
  `Create`, and writes `NoteOutsideExchange` as `#- * <text>` (ADR-0028). Add a level-aware
  factory in `Surl.Output` (a new type named for what it does, e.g. `LevelledExchangeLogFactory`,
  taking the level enum this task defines in `Surl.Output`, the writer, the timestamp switch
  and a `TimeProvider`). Keep `VerboseExchangeLogFactory`'s public constructor working,
  because `Surl.Console/CommandLineRunner.cs` constructs it in `ServeSecuredAsAskedAsync` and
  `Surl.Console` is outside this task; BL-107 switches `surl` over and removes whatever is
  then unused.
- `VerboseExchangeLog.cs` writes `#<id> <marker> <text>` lines under a shared `Lock`;
  `ExchangeLogEscaping.cs` escapes (ADR-0006 section 3). The info line is derived from the
  engine's `Connection from ...`/`Flow from ...` and `Closed` notes only (ADR-0033
  decision 3); no `IExchangeLog` member is added.
- Time: inject `TimeProvider` (root `CLAUDE.md`); tests use a fixed fake provider with a
  fixed local time zone, never the wall clock.
- Tests: `Surl.Output.UnitTests/VerboseExchangeLogFactoryTests.cs`, `VerboseLogEscapingTests.cs`.

## Acceptance criteria

- [ ] Tests in `Surl.Output.UnitTests` prove, for one scripted exchange (a connection note,
      received and sent bytes, a note, `Closed`) and one datagram flow: level none writes
      nothing; error writes only what ADR-0033 calls an error; info writes exactly ADR-0033's
      info line(s); verbose writes today's lines unchanged (the existing
      `VerboseExchangeLogFactoryTests` still pass, adapted only for the constructor).
- [ ] With timestamps on, a test using a fake `TimeProvider` pins ADR-0033's timestamp
      format on each line ADR-0033 says gets one, including `NoteOutsideExchange` lines.
- [ ] Lines from two concurrent exchanges never interleave mid-line (an existing-style
      concurrency test at the info level).
- [ ] `dotnet build Surl.Output.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Surl.Output.UnitLibrary` keeps 100% line and branch coverage and no method exceeds
      complexity 10.

## Notes

## Log

- 2026-09-29: Created.
