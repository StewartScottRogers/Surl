---
id: BL-104
title: Parse -s, -S, --log-level, --trace, --trace-ascii, --trace-time and --log-file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-101, BL-103, BL-105]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-013
created: 2026-09-29
completed: 2026-09-29
---
# BL-104 — Parse -s, -S, --log-level, --trace, --trace-ascii, --trace-time and --log-file

## Goal

`Surl.Cli` parses `-s`/`--silent`, `-S`/`--show-error`, `--log-level`, `--trace`,
`--trace-ascii`, `--trace-time` and `--log-file` into `SurlCommandLine` exactly as
ADR-0033 decides, with their help entries in ADR-0034's categories.

## Context

FR-013; ADR-0033 (BL-101) gives each option's syntax, argument kind, negatability, error
texts and how the level options combine; ADR-0034 (BL-102) gives their categories and
descriptions. BL-103 built the categorised help table this task adds entries to.

- `Surl.Cli.UnitLibrary/CommandLineOptions.cs`: the option table (`Flag`,
  `WithArgument<T>`); `OptionArgumentReader.cs` has `ReadPath` for `<file>` arguments.
- `Surl.Cli.UnitLibrary/SurlCommandLine.cs`: today `bool Verbose`. Replace it with the
  member(s) ADR-0033 names (the level, typed as the level enum BL-105 defined in
  `Surl.Output.UnitLibrary` - `Surl.Cli` already references `Surl.Output`, so there is one
  level type, not two - plus the trace file and its kind, the timestamp switch and the log
  file); `-v` keeps working as the verbose level. `Surl.Console/CommandLineRunner.cs` reads `commandLine.Verbose` in two places: keep
  it compiling in this task by mapping the new level back to what the runner does today
  (verbose or not), and leave the composition of the new levels to BL-107 - if removing
  `Verbose` breaks `Surl.Console`, keep `Verbose` as a derived read-only property instead
  (this task touches only `Surl.Cli` and its tests).
- Tests: `Surl.Cli.UnitTests/CommandLineParserTests.cs`, `HelpTextTests.cs`.

## Acceptance criteria

- [x] `CommandLineParserTests` prove, for each option, the parsed value and every error text
      ADR-0033 gives (missing argument, empty `<file>`, bad `--log-level` word, `=value` on a
      flag, `--no-` where not negatable), each refused with `SurlExitCode.FailedInit` and the
      `try 'surl --help' for more information` line where ADR-0007 section 5 says.
- [x] `CommandLineParserTests` prove ADR-0033's combination rule with named cases at least
      for `-s`, `-s -S`, `-sS`, `-v`, `-s -v`, `-v --log-level error`, `--log-level trace`
      without `--trace`, and `--trace f --trace-ascii g`.
- [x] A parsed command line with none of the options has the default level (info) and no
      trace or log file.
- [x] `HelpTextTests` pin each new option's help lines in the categories ADR-0034 names.
- [x] `dotnet build Surl.Cli.UnitLibrary -warnaserror` and `dotnet build Surl.Console
      -warnaserror` are clean; the fast tests pass; `Surl.Cli.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

- `SurlCommandLine` gains `LogLevel` (`Surl.Output.LogLevel`, default `Info`), `ShowError`,
  `TraceFile`, `TraceLayout` (`Surl.Output.TraceDumpLayout`, default `HexAndAscii`),
  `TraceTime` and `LogFile`. `Verbose` stays as a derived read-only property
  (`LogLevel == Verbose`) because `Surl.Console/CommandLineRunner.cs` reads it and this task
  touches only `Surl.Cli`; at `trace` the runner therefore writes nothing extra until BL-107
  composes the levels.
- Choice (ADR-0033 section 2, "used only when the final level is trace"): the parser sets
  `TraceFile` to null unless the final level is `Trace`, so `--trace f -v` and
  `--trace f -s -S` hand BL-107 no file to open. `TraceLayout` keeps the last trace option's
  layout either way; it matters only at `Trace`.
- Choice: `-S` is kept as `ShowError` on the record (a flag needs somewhere to land) and
  applied in `CommandLineParser.Finish`, after the whole line, so `-S -s` is `error`.
- `--log-level` has its own reader, `OptionArgumentReader.ReadLogLevel`: empty is `Blank`,
  any other unknown word `BadlyUsed`, words case-insensitive.
- The two "curl option surl leaves out" rows of `Parse_UnknownOption_IsRefused` used `-s` and
  `--silent`; they now use `-k` and `--insecure`.
- No new ADR: every behaviour here is ADR-0033's and ADR-0034's.
- Verified 2026-09-29: `dotnet build -warnaserror` clean (solution, so `Surl.Cli.UnitLibrary`
  and `Surl.Console`); `dotnet format --verify-no-changes` clean; fast tests all green
  (Surl.Cli.UnitTests 449); `Surl.Cli.UnitLibrary` line and branch rate 1 (cobertura).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl parses -s, -S, --log-level, --trace, --trace-ascii, --trace-time and --log-file into SurlCommandLine and lists them in help's logging category
