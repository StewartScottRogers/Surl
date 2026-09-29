---
id: BL-126
title: Remove the unused VerboseExchangeLogFactory from Surl.Output
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-107]
touches: [Surl.Output.UnitLibrary, Surl.Output.UnitTests]
requirement: FR-013
created: 2026-09-29
completed: 2026-09-29
---
# BL-126 — Remove the unused VerboseExchangeLogFactory from Surl.Output

## Goal

`Surl.Output.UnitLibrary` no longer has `VerboseExchangeLogFactory`: `LevelledExchangeLogFactory` and `TraceExchangeLogFactory` are the only exchange log factories left.

## Context

`Surl.Output.UnitLibrary/VerboseExchangeLogFactory.cs` wraps a `LevelledExchangeLogFactory` at `LogLevel.Verbose` or `LogLevel.None` with no timestamps. Its own remarks say it exists "for `surl` until it passes the parsed log level (BL-107)". BL-107 composed ADR-0033's log levels in `Surl.Console` through `LevelledExchangeLogFactory` and `TraceExchangeLogFactory`, so `Surl.Console` no longer constructs it and nothing in production uses it. This is a removal only. The `-v` behaviour (FR-013, ADR-0007 section 8, ADR-0033 section 1) does not change, and `VerboseExchangeLog` stays because `LevelledExchangeLogFactory` builds on it.

These test files still use the factory and must be changed. None of them needs the factory itself:
- `Surl.Output.UnitTests/VerboseExchangeLogFactoryTests.cs`: delete it. Before deleting, check that every behaviour it pins that is still live (line layout, `verbose: false` writing nothing, `NoteOutsideExchange`) is also pinned by a `LevelledExchangeLogFactoryTests` test at `LogLevel.Verbose` or `LogLevel.None`. Move any case that is not.
- `Surl.Output.UnitTests/VerboseLogEscapingTests.cs` (around lines 56, 73 and 74): build the logs with `new LevelledExchangeLogFactory(writer, LogLevel.Verbose, stampTimes: false, TimeProvider.System)` in place of `new VerboseExchangeLogFactory(writer, verbose: true)`.
- `Surl.Output.UnitTests/LevelledExchangeLogFactoryTests.cs`, `Verbose_ScriptedExchanges_WritesEveryEventAsTheVerboseLogDoes` (around line 85): the test uses the factory's output as its expected value. Remove that comparison and keep the literal `Lines(...)` assertion that already follows it. Rename the test if its name no longer says what it checks.

ADRs are history. Mentions in `Documentation/Planning/Decisions/ADR-0028-…` and `ADR-0033-…` and in finished tasks under `Tasks/Done` stay as they are.

## Acceptance criteria

- [x] `Surl.Output.UnitLibrary/VerboseExchangeLogFactory.cs` and `Surl.Output.UnitTests/VerboseExchangeLogFactoryTests.cs` no longer exist.
- [x] Searching the repository for `VerboseExchangeLogFactory` finds nothing in any `.cs`, `.csproj`, `CLAUDE.md`, `README.md` or `Documentation/Wiki` file. Matches remain only in `Documentation/Planning/Decisions/` ADRs and in task files.
- [x] No doc comment in `Surl.Output.UnitLibrary` (including the `<see cref>`s in `LevelledExchangeLogFactory.cs` and `VerboseExchangeLog.cs`), and neither `Surl.Output.UnitLibrary/CLAUDE.md` nor `Surl.Output.UnitTests/CLAUDE.md` if present, describes the factory as live code.
- [x] `dotnet build Surl.Output.UnitLibrary -warnaserror` and `dotnet build Surl.Output.UnitTests -warnaserror` are clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes, and no test needs `TestCategory=Integration`.
- [x] Surl.Output.UnitLibrary is still at 100% line and 100% branch coverage, measured by `Measure-CodeQuality.ps1`.

## Notes

- Every case in the deleted `VerboseExchangeLogFactoryTests` that the scripted-exchange tests in `LevelledExchangeLogFactoryTests` did not already pin was moved there at `LogLevel.Verbose` (head lines, status line, trailing bytes, escaping, empty input, 1024-byte split, outside-exchange control bytes, null remote end point at None and Verbose, and the no-interleaving check). `Verbose_ScriptedExchanges_WritesEveryEventAsTheVerboseLogDoes` became `Verbose_ScriptedExchanges_WritesEveryEvent`: it now checks only the literal lines.
- Measured: Surl.Output.UnitLibrary 100% line, 100% branch, 0 failing members; Surl.Output.UnitTests 95 tests passing.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. VerboseExchangeLogFactory is gone; LevelledExchangeLogFactory and TraceExchangeLogFactory are the only exchange log factories, with its live cases moved to LevelledExchangeLogFactoryTests
