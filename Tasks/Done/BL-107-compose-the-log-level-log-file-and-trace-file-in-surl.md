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
completed: 2026-09-29
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

- [x] `CommandLineRunnerTests` prove, each by name, with a fake listener factory and
      `StringWriter`s: `-s` writes nothing to `output` or `error` for a normal start and for
      a start that fails (e.g. an unsupported scheme still returns
      `SurlExitCode.UnsupportedProtocol`); `-s -S` writes only the failure's `surl: ` line;
      the default writes the `Listening on` lines and ADR-0033's info line per connection;
      `-v` writes today's verbose lines.
- [x] A test proves `--trace <file>` sends the dump to the writer the runner's file seam
      returned for that path, and `--trace -` to `output`; `--log-file <file>` sends the log
      to its writer and nothing to `error` (or as ADR-0033 decision 6 says).
- [x] A test proves a trace or log file that cannot be opened ends surl before any listener
      binds with ADR-0033's exit code and exact `surl: ` text.
- [x] A test proves `--trace-time` stamps come from the runner's injected `TimeProvider`.
- [x] `dotnet build Surl.Console -warnaserror` is clean; the fast tests pass; `Surl.Console`
      keeps 100% line and branch coverage, the real-file path being
      `[TestCategory("Integration")]` only if it cannot be covered otherwise.

## Notes

- Plan, as built: `Surl.Console/LogStreams.cs` opens the log stream (stderr, `--log-file`
  appended with `FileMode.Append`, `-` stdout) and the trace file (`FileMode.Create`,
  truncated; `-` stdout) and builds `LevelledExchangeLogFactory` below trace and
  `TraceExchangeLogFactory` at trace (to the trace file, else the log stream). The runner's
  new optional seam `openLogFile: Func<string, FileMode, TextWriter>` defaults to
  `LogFile.Open` (the only real-disk code, `[ExcludeFromCodeCoverage]` and covered by the
  Integration tests in `LogFileTests`, as `DataDirectoryProbe` and `DataDirectoryLock` are).
- Files are opened after TLS is composed and before the listener factory is created, so a
  refusal earlier on (scheme, directory, lock, TLS file) never creates or truncates a log
  file; an unopenable one ends surl with 23 and
  `surl: (23) Could not open <path> for <option>: <message>` (ADR-0033 section 6), closing
  any file already opened. Only `IOException` and `UnauthorizedAccessException` are caught,
  as `LoadRetainedMessagesAsync` does.
- `-s`: the runner hands `TextWriter.Null` as stderr to everything after a successful parse,
  so every `surl: ` failure is hidden while a command-line refusal is still written. The
  status lines are written to `TextWriter.Null` below `info`. The throwaway-certificate note
  goes to the log stream at `verbose` and `trace`.
- Startup warnings (ADR-0033 section 7) are not written anywhere yet: no code writes
  ADR-0032's `surl: warning:` lines. BL-116 and BL-117 add them; they belong on
  `LogStreams.Log` at `info` and above.
- The tests live in `Surl.Console.UnitTests/CommandLineRunnerLogTests.cs`, beside
  `CommandLineRunnerTlsTests` and `CommandLineRunnerRetainedMessagesTests`, rather than in
  the 700-line `CommandLineRunnerTests.cs`. A `FakeConnection` handed out by
  `FakeListenerFactory.Connection` drives one real HTTP exchange through the engine.
- After the switch-over `VerboseExchangeLogFactory` (Surl.Output) and
  `SurlCommandLine.Verbose` (Surl.Cli) are unused; their removal is filed as BL-126 and
  BL-127.
- Code review (code-reviewer): nothing had to be fixed. Taken from its suggestions: the
  open-failure filter also catches `ArgumentException` and `NotSupportedException` (a path
  the file system rejects is still a 23, not a crash); `LogFile.Open` shares
  `FileShare.Read | FileShare.Delete` so a log can be rotated on Windows while surl runs;
  the fake factory hands its connection out with `Interlocked.Exchange`; tests added for
  `--trace f -v` never opening `f` and `-s -S` with an unopenable file. Filed as BL-128:
  `--log-file` and `--trace` naming the same file (Windows refuses the second open, Linux
  and macOS truncate the log), which ADR-0033 does not decide.
- Verified: `dotnet build -warnaserror` clean; fast tests green (Surl.Console.UnitTests 128
  fast, plus 3 Integration `LogFileTests` passing); `Measure-CodeQuality.ps1 -Library
  Surl.Console` on the fast run: 100% line, 100% branch, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl honours -s, -s -S, info, -v and --trace/--trace-ascii levels, --trace-time stamps and --log-file, per ADR-0033
