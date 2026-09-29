---
id: BL-128
title: Decide and handle --log-file and --trace naming the same file
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-107]
touches: [Surl.Console, Surl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: FR-013
created: 2026-09-29
completed: 2026-09-29
---
# BL-128 — Decide and handle --log-file and --trace naming the same file

## Goal

When `--log-file` and `--trace` (or `--trace-ascii`) name the same file, `surl` does the one thing an ADR decides, the same way on Windows, Linux and macOS, instead of failing on Windows and silently losing log lines elsewhere.

## Context

- BL-107 opens the `--log-file` (`FileMode.Append`) and the trace file (`FileMode.Create`) separately in `Surl.Console/LogStreams.cs` (`LogStreams.Open` -> `Choose`), each through the `openLogFile` seam, which `surl` fills with `LogFile.Open` (`Surl.Console/LogFile.cs`, `FileShare.Read | FileShare.Delete`). `CommandLineRunner` takes the seam as its `openLogFile` constructor parameter (`Func<string, FileMode, TextWriter>`).
- When both options name the same file - `--log-file x --trace x`, or two spellings of one full path such as `x` and `./x` - Windows refuses the second open with a sharing violation, so `surl` exits 23 (`SurlExitCode.CouldNotWriteFile`). On Linux and macOS both opens succeed, the trace's truncate empties the log, and the two writers overwrite each other's bytes.
- `Documentation/Planning/Decisions/ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md` section 6 (`--log-file <file>`) and section 4 (the `--trace` dump) do not cover the collision. Code review of BL-107 found it.
- This is a Surl-only option pairing: upstream curl has no `--log-file`, so there are no upstream bytes to measure. Decide by the standing rules in root `CLAUDE.md`, "Decisions" (simplest behaviour that stays a faithful mate; base class library only). Candidates: refuse before any listener binds with a stated `SurlExitCode` and exact `surl: (N) ...` text, or open the file once and share one writer between the log and the trace.
- Existing exit codes are in `SurlExitCode` (e.g. `FailedInit = 2`, `CouldNotWriteFile = 23`); a new value, if the ADR wants one, follows that enum's numbering rule.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/` (next free number after the highest there, listed in that folder's `README.md`), marked "Decided by Claude under Stewart's delegation", picks one behaviour for every platform when `--log-file` and `--trace`/`--trace-ascii` resolve to the same file, comparing `Path.GetFullPath` of both paths; if it refuses, it states the `SurlExitCode`, the exact `surl:` text, and that the refusal happens before any listener binds; ADR-0033 section 6 links to it.
- [x] `Surl.Console` implements that behaviour in `LogStreams.cs` (or `CommandLineRunner.cs`), with the same result on Windows, Linux and macOS.
- [x] A test in `Surl.Console.UnitTests/CommandLineRunnerLogTests.cs` proves it through the `openLogFile` seam, for both the identical spelling (`--log-file x --trace x`) and two spellings of one full path, and asserts the exit code and `surl:` text (or the shared writer) the ADR names; the test is platform-neutral (no drive letters) and needs no `TestCategory=Integration`.
- [x] A test proves two different files still open separately, as BL-107 left them.
- [x] `dotnet build Surl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] `Surl.Console` stays at 100% line and 100% branch coverage.

## Notes

- Decision (ADR-0037, decided by Claude under Stewart's delegation): refuse, not share one
  writer. The log is appended and the trace truncated, so one file cannot keep both promises,
  and `--log-file x --log-level trace` already puts the dump in the log file.
- Exit code 23 (`CouldNotWriteFile`), not 2: the refusal happens where every other log-file
  failure happens (`LogStreams.Open`, before any listener binds), with the same message shape:
  `surl: (23) Could not open <trace path> for <option>: --log-file names the same file`. The
  log file already opened is closed again.
- Paths compare by `Path.GetFullPath` with `StringComparison.OrdinalIgnoreCase` on every
  platform, so Windows and macOS's case-insensitive volumes refuse alike; the cost is refusing
  a case-only difference on case-sensitive Linux, recorded in the ADR.
- The comparison runs inside `Choose`'s existing `try`, so a path `GetFullPath` rejects gets
  the existing `(23)` message with no new branch.
- Pipeline: a one-method change in one project, so the feature stages ran in-session (decide,
  test, implement, verify) rather than through separate agents.
- Tests: `RunAsync_TraceFileIsTheLogFile_...` (5 rows: same spelling, `./`, `logs/../`, upper
  case, `--trace-ascii`), `RunAsync_LogFileAndADifferentTraceFile_OpensEachSeparately`,
  `RunAsync_LogFileToStdoutAndATraceFile_OpensOnlyTheTraceFile`. Surl.Console.UnitTests 153
  passed, whole fast suite green; `Measure-CodeQuality.ps1 -Library Surl.Console` reports 0
  failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A --trace or --trace-ascii file with the --log-file file's full path is refused with 23 before any listener binds, alike on every platform (ADR-0037)
