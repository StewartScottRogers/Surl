---
id: BL-019
title: Compose surl in Surl.Console so it serves HTTP from a directory
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-010, BL-011, BL-014, BL-015, BL-016, BL-018]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs, README.md, DOWNLOAD.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-019 — Compose surl in Surl.Console so it serves HTTP from a directory

## Goal

`surl [options] http://<host>:<port>/` serves the files of the served directory over
HTTP/1.1 until Ctrl+C. This replaces the Phase 0 placeholder: `Surl.Console` wires the
command-line parser, serving engine, TCP listener, content store, output writers and
HTTP server by explicit construction.

## Context

- `Surl.Console/CLAUDE.md`: Phase 1 makes it the composition root, with explicit
  dependency injection and never assembly scanning. Keep it thin.
  `Surl.Console/Program.cs` today writes `surl: not implemented yet` and returns
  `SurlExitCode.FailedInit`, and `Surl.Console.UnitTests/ProgramTests.cs` pins that.
- The pieces: command-line parser (BL-013, BL-014), serving engine (BL-015), TCP
  listener (BL-011), `System.IO` content store (BL-008 to BL-010), status line and
  verbose log (BL-016), HTTP server (BL-018). The command-line ADR (BL-003) and
  exit-code ADR (BL-001) give the texts and codes. Both are indexed in
  `Documentation/Planning/Decisions/README.md`.
- `Surl.Conformance.UnitTests` already has `InternalsVisibleTo` from `Surl.Console`.
  BL-020 needs to start surl in-process and stop it, so expose an internal entry point
  that takes the arguments, the output and error writers and a `CancellationToken`, and
  returns the exit code. `Main` wires `Console.CancelKeyPress` (and the process-exit
  signal) to that token.
- `SurlExitCode.FailedInit`'s doc comment says the Phase 0 placeholder returns it for
  every command line. Once this lands that is false, so rewrite it to the meaning the
  exit-code ADR gives (root `CLAUDE.md`: a misaligned document is a defect).
- `https` is not wired here, because TLS (BL-012) is not a dependency. A listen URL with
  a scheme that has no registered server gets the exit code the ADR assigns.
- `Surl.Console` is held to the same 100% gates. Fast tests use fakes for the listener
  factory, and the real-socket path is `[TestCategory("Integration")]`.

## Acceptance criteria

- [x] `Program.Main` delegates to an internal entry point taking
      `(string[] args, TextWriter output, TextWriter error, CancellationToken)`, and the
      placeholder message is gone.
- [x] Construction is explicit: no reflection, no assembly scanning. `dotnet publish
      Surl.Console -c Release` still succeeds with native AOT (vswhere on PATH as
      `CLAUDE.md` describes) with no trim or AOT warnings.
- [x] Fast tests in `Surl.Console.UnitTests` prove: `--help` and `--version` write the
      ADR's text and return `SurlExitCode.Ok`; an unknown option returns the ADR's code
      and message on the error writer; an unsupported scheme returns the ADR's code;
      with a fake listener factory, a listen URL starts one listener, the status line is
      written, and cancelling the token returns the normal-stop code.
- [x] `ProgramTests.Main_AnyCommandLine_WritesNotImplementedAndReturnsFailedInit` is
      removed or replaced, and `Main_NullArguments_ThrowsArgumentNullException` still
      passes.
- [x] One `[TestCategory("Integration")]` test serves a temporary directory holding
      `hello.txt` on `http://127.0.0.1:0/`, reads the bound port from the status line,
      fetches `/hello.txt` with `System.Net.Http.HttpClient` (a smoke test only; upstream
      curl is BL-020's job), gets the file's bytes, then cancels and gets the
      normal-stop code.
- [x] `SurlExitCode.FailedInit`'s XML doc no longer mentions the Phase 0 placeholder.
      `Surl.Console/CLAUDE.md` no longer calls the project a placeholder.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.

## Notes

- **Shape.** `Program.Main` guards `args`, registers SIGINT and SIGTERM, and calls
  `Program.RunAsync(args, output, error, cancellationToken)`, which builds a
  `CommandLineRunner` over the real listener factory, `ServedDirectoryProbe.CanOpen` and
  `TimeProvider.System`. `CommandLineRunner` is the composition root: parse, help or
  version, served-directory check, scheme check, then content store, `HttpProtocolServer`,
  `VerboseExchangeLogFactory` and `ServingEngine`, built explicitly. The ADR-0007 section 5
  order (parse, directory, schemes, listeners) is kept.
- **Signals.** `PosixSignalRegistration` for SIGINT and SIGTERM rather than
  `Console.CancelKeyPress`: SIGINT is Ctrl+C on every platform (Windows included), one API
  covers both signals, and `PosixSignalContext` has a public constructor, so the handler
  (`Program.CreateStopOnSignal`) is unit tested. The handler cancels the default handling,
  so the engine shuts down gracefully and `surl` exits 0 (ADR-0005 section 2).
- **Status lines and bind messages** need information the serving engine does not hand
  back, so `ListenerStartReporter` decorates the listener factory: it writes one status
  line per listener once the last has bound (ADR-0007 section 7: "after all listeners
  bound"), and keeps the `ListenerBindException` for the `(45)`/`(6)` texts. This keeps
  `Surl.Core` untouched.
- **Listener factory stop-gap.** `Surl.Networking` has no `IListenerFactory` yet (BL-055
  waits on UDP, BL-031), so `Surl.Console/TcpListenerFactory.cs` starts
  `TcpConnectionListener`s and refuses datagram listeners with `NotSupportedException`; the
  engine never asks for one because no datagram server is registered. BL-062 filed to
  swap in BL-055's factory and delete the stop-gap.
- **Served directory check** is a seam (`Func<string, bool>`) so the fast tests touch no
  disk (`.claude/rules/testing.md`); the real probe opens the directory and reads its first
  entry, returning false on `IOException` or `UnauthorizedAccessException`, and is covered
  by Integration tests (excluded from coverage with a justification, the
  `DiskContentFileSystem` precedent).
- **`--version` schemes** come from composing the protocol servers (over the current
  directory, never used), so the `Protocols:` line is exactly what is registered: `http`.
- **Not in scope, already filed:** option limits reaching `ExchangeContext.Limits`
  (BL-059, BL-025); the exposure flags (`--list-directories` etc.) are parsed but have no
  effect until their tasks (BL-044 and others) land.
- **Touches widened** to `README.md` and `DOWNLOAD.md`: both said `surl` "only prints
  `surl: not implemented yet` and exits 2", which this task made false (root `CLAUDE.md`:
  a misaligned document is a defect). No task in `Doing` names either file.
- **Verified:** `dotnet build -warnaserror` clean; fast tests green (Surl.Console.UnitTests
  27 fast, 31 with Integration, all passing); `dotnet publish Surl.Console -c Release`
  native AOT with no warnings, and the native `surl.exe` answered `--version` (0),
  a missing `--directory` (37) and `https://` (1) with ADR-0007's texts;
  `Measure-CodeQuality.ps1 -Library Surl.Console`: 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. surl serves a directory over HTTP/1.1 until Ctrl+C or SIGTERM, with ADR-0007's texts and exit codes
