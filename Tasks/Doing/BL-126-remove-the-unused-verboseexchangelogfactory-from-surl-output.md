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
completed:
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

- [ ] `Surl.Output.UnitLibrary/VerboseExchangeLogFactory.cs` and `Surl.Output.UnitTests/VerboseExchangeLogFactoryTests.cs` no longer exist.
- [ ] Searching the repository for `VerboseExchangeLogFactory` finds nothing in any `.cs`, `.csproj`, `CLAUDE.md`, `README.md` or `Documentation/Wiki` file. Matches remain only in `Documentation/Planning/Decisions/` ADRs and in task files.
- [ ] No doc comment in `Surl.Output.UnitLibrary` (including the `<see cref>`s in `LevelledExchangeLogFactory.cs` and `VerboseExchangeLog.cs`), and neither `Surl.Output.UnitLibrary/CLAUDE.md` nor `Surl.Output.UnitTests/CLAUDE.md` if present, describes the factory as live code.
- [ ] `dotnet build Surl.Output.UnitLibrary -warnaserror` and `dotnet build Surl.Output.UnitTests -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes, and no test needs `TestCategory=Integration`.
- [ ] Surl.Output.UnitLibrary is still at 100% line and 100% branch coverage, measured by `Measure-CodeQuality.ps1`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
