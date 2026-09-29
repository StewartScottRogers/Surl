---
id: BL-096
title: Refuse a second surl on a directory another surl holds, through its .surl/lock file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-090, BL-093]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Protocol.Abstractions.UnitLibrary]
requirement: FR-025
created: 2026-09-29
completed:
---
# BL-096 — Refuse a second surl on a directory another surl holds, through its .surl/lock file

## Goal

A `surl --directory <path>` holds `<path>/.surl/lock` open exclusively for its lifetime,
and a second `surl` given the same path while the first runs is refused before any
listener binds, with ADR-0031's stderr text and `SurlExitCode`.

## Context

FR-025; ADR-0031 (BL-090) decision 7 gives the lock path, the open mode, whether the file
is deleted on exit, where in `ServeAsync` it is taken, and the refusal's exact text and
exit code. Decision 3 covers a `.surl` folder that cannot be created.

- Put the disk code in a new `Surl.Console/DataDirectoryLock.cs` (or the name ADR-0031
  uses), modelled on `Surl.Console/ServedDirectoryProbe.cs`: a thin method that opens the
  file with `FileMode.OpenOrCreate`, `FileAccess.ReadWrite`, `FileShare.None`, returning an
  `IDisposable` holder or a refusal, excluded from coverage with a justifying comment and
  proved by `Integration` tests.
- `Surl.Console/CommandLineRunner.cs` takes it as a constructor delegate, as it takes
  `canOpenServedDirectory`, so `CommandLineRunnerTests` drive the refusal with a fake;
  `Program.RunAsync` passes the real one. The holder is disposed when serving ends,
  whatever the exit path.
- No lock is taken without `--directory`, for `--help` or for `--version`.
- A lock taken with `FileShare.None` in .NET is an advisory `flock` on Linux and macOS and
  a share-mode lock on Windows; both refuse a second open in another process and in the
  same process, and both vanish when the holder dies, so a stale file never refuses a
  start.
- If ADR-0031 adds a `SurlExitCode` member, add it to
  `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs` in this task, with its XML doc
  comment citing ADR-0031 (that is why this task touches the abstractions library).

## Acceptance criteria

- [ ] `CommandLineRunnerTests` prove, by name: with `--directory` the lock is taken before
      the listener factory is asked for any listener; a refused lock returns ADR-0031's
      `SurlExitCode` and writes `surl: ` plus its exact text to stderr, and no listener is
      started; the holder is disposed after serving ends normally, after a bind failure and
      after an internal error; without `--directory`, `--help` and `--version`, the lock
      delegate is never called.
- [ ] `Integration` tests in `DataDirectoryLockTests`: taking the lock twice on one
      temporary directory refuses the second; after the first is disposed a new one
      succeeds; a lock file left on disk with no holder does not refuse; the `.surl`
      folder is created when missing.
- [ ] `dotnet build Surl.Console -warnaserror` is clean, the fast tests pass, and
      `Surl.Console` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
