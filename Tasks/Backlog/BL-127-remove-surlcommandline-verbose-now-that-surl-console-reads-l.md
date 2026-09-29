---
id: BL-127
title: Remove SurlCommandLine.Verbose now that Surl.Console reads LogLevel
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-107]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-013
created: 2026-09-29
completed:
---
# BL-127 — Remove SurlCommandLine.Verbose now that Surl.Console reads LogLevel

## Goal

`SurlCommandLine` has no `Verbose` property. `LogLevel` is the only way to read what `-v`, `-s`, `-S`, `--log-level` and `--trace*` chose.

## Context

`Surl.Cli.UnitLibrary/SurlCommandLine.cs` (around lines 43-46) declares `public bool Verbose => LogLevel == LogLevel.Verbose;`. Its doc comment says `Surl.Console` reads it until `Surl.Console` composes the levels. BL-107 composed ADR-0033's log levels in `Surl.Console`, which now reads `LogLevel` only (for example `commandLine.LogLevel >= LogLevel.Verbose` in `Surl.Console/CommandLineRunner.cs`). Nothing in production reads `Verbose` any more. This is a removal only. Parsing does not change: `-v` still sets `LogLevel.Verbose` and `--no-verbose` still sets `LogLevel.Info` (`Surl.Cli.UnitLibrary/CommandLineOptions.cs`, FR-013, ADR-0033 section 1).

`Surl.Cli.UnitTests/CommandLineParserTests.cs` reads `.Verbose` in these places. Rewrite each one to assert on `LogLevel`:
- The `FlagValues` table entry `["verbose"] = c => c.Verbose` (line 14) becomes `c => c.LogLevel == LogLevel.Verbose`. The negatable-flag tests then keep checking that `--verbose` gives `LogLevel.Verbose` and `--no-verbose` gives `LogLevel.Info`.
- `Assert.IsFalse(defaults.Verbose)` (around line 51) and `Assert.IsFalse(commandLine.Verbose)` (around line 84) become `Assert.AreEqual(LogLevel.Info, ….LogLevel)`. If `LogLevel` is already asserted beside them, just delete the line.
- `Assert.AreEqual(expected == LogLevel.Verbose, commandLine.Verbose)` (around line 119) is deleted. The data-row test already asserts `LogLevel` against `expected`.
- `Assert.IsTrue(commandLine.Verbose)` (around lines 323 and 726) and `Assert.IsTrue(Served(Url, "-v").Verbose)` (around line 715) become `Assert.AreEqual(LogLevel.Verbose, ….LogLevel)`.

Line numbers are from the tree at filing time. Search for `.Verbose` rather than trusting them.

## Acceptance criteria

- [ ] `Surl.Cli.UnitLibrary/SurlCommandLine.cs` no longer declares `Verbose`, and no doc comment in `Surl.Cli.UnitLibrary` refers to it.
- [ ] Searching all `.cs` files for `\.Verbose\b` finds only `LogLevel.Verbose`. No `SurlCommandLine.Verbose` read is left in any project.
- [ ] `CommandLineParserTests` asserts `LogLevel.Verbose` for `-v`/`--verbose` and `LogLevel.Info` for `--no-verbose` and for the defaults, through the `["verbose"]` `FlagValues` entry and the rewritten assertions.
- [ ] `dotnet build Surl.Cli.UnitLibrary -warnaserror`, `dotnet build Surl.Cli.UnitTests -warnaserror` and `dotnet build Surl.Console -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes, and no test needs `TestCategory=Integration`.
- [ ] Surl.Cli.UnitLibrary is still at 100% line and 100% branch coverage, measured by `Measure-CodeQuality.ps1`.

## Notes

## Log

- 2026-09-29: Created.
