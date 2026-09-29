---
id: BL-107
title: Compose the log level, --log-file and --trace file in surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-104, BL-106]
touches: [Surl.Console, Surl.Console.UnitTests]
requirement: FR-013
created: 2026-09-29
completed:
---
# BL-107 — Compose the log level, --log-file and --trace file in surl

## Goal

`surl` honours the parsed log level: `-s` writes nothing, `-s -S` only errors, the default
info level the `Listening on` lines, startup warnings and one line per connection, `-v` the
verbose log, `--trace`/`--trace-ascii` a byte dump to the named file (or `-`), with
`--trace-time` stamps and `--log-file` redirecting the log - every stream and text as
ADR-0033 says.

## Context

FR-013; ADR-0033 (BL-101) decisions 1, 6 and 7. BL-104 parses the options into
`SurlCommandLine`; BL-105 and BL-106 built the writers in `Surl.Output`.

- `Surl.Console/CommandLineRunner.cs`: `ServeSecuredAsAskedAsync` builds
  `new VerboseExchangeLogFactory(error, commandLine.Verbose)` and writes the throwaway note
  with `-v` only; `WriteFailure`/`WriteRefusal` write every `surl: ` message to `error`;
  `ListenerStartReporter` + `ListenerStatusLine(output)` write the status lines.
- `Surl.Console/Program.cs` passes `System.Console.Out`/`Error`; files are opened in
  `Surl.Console` only (it is where real-disk code lives, beside `DataDirectoryLock`), behind
  a delegate or seam the runner is given so `Surl.Console.UnitTests` needs no disk, as
  `canOpenDataDirectory` and `takeDataDirectoryLock` do today.
- After the switch-over `VerboseExchangeLogFactory` may be unused; removing it from
  `Surl.Output` is outside `touches`, so if it is dead, have `task-planner` file its removal
  as a follow-up task rather than widening this one.
- Tests: `Surl.Console.UnitTests/CommandLineRunnerTests.cs` with `FakeListenerFactory`.

## Acceptance criteria

- [ ] `CommandLineRunnerTests` prove, each by name, with a fake listener factory and
      `StringWriter`s: `-s` writes nothing to `output` or `error` for a normal start and for
      a start that fails (e.g. an unsupported scheme still returns
      `SurlExitCode.UnsupportedProtocol`); `-s -S` writes only the failure's `surl: ` line;
      the default writes the `Listening on` lines and ADR-0033's info line per connection;
      `-v` writes today's verbose lines.
- [ ] A test proves `--trace <file>` sends the dump to the writer the runner's file seam
      returned for that path, and `--trace -` to `output`; `--log-file <file>` sends the log
      to its writer and nothing to `error` (or as ADR-0033 decision 6 says).
- [ ] A test proves a trace or log file that cannot be opened ends surl before any listener
      binds with ADR-0033's exit code and exact `surl: ` text.
- [ ] A test proves `--trace-time` stamps come from the runner's injected `TimeProvider`.
- [ ] `dotnet build Surl.Console -warnaserror` is clean; the fast tests pass; `Surl.Console`
      keeps 100% line and branch coverage, the real-file path being
      `[TestCategory("Integration")]` only if it cannot be covered otherwise.

## Notes

## Log

- 2026-09-29: Created.
