---
id: BL-019
title: Compose surl in Surl.Console so it serves HTTP from a directory
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-010, BL-011, BL-014, BL-015, BL-016, BL-018]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs]
requirement: none
created: 2026-09-28
completed:
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

- [ ] `Program.Main` delegates to an internal entry point taking
      `(string[] args, TextWriter output, TextWriter error, CancellationToken)`, and the
      placeholder message is gone.
- [ ] Construction is explicit: no reflection, no assembly scanning. `dotnet publish
      Surl.Console -c Release` still succeeds with native AOT (vswhere on PATH as
      `CLAUDE.md` describes) with no trim or AOT warnings.
- [ ] Fast tests in `Surl.Console.UnitTests` prove: `--help` and `--version` write the
      ADR's text and return `SurlExitCode.Ok`; an unknown option returns the ADR's code
      and message on the error writer; an unsupported scheme returns the ADR's code;
      with a fake listener factory, a listen URL starts one listener, the status line is
      written, and cancelling the token returns the normal-stop code.
- [ ] `ProgramTests.Main_AnyCommandLine_WritesNotImplementedAndReturnsFailedInit` is
      removed or replaced, and `Main_NullArguments_ThrowsArgumentNullException` still
      passes.
- [ ] One `[TestCategory("Integration")]` test serves a temporary directory holding
      `hello.txt` on `http://127.0.0.1:0/`, reads the bound port from the status line,
      fetches `/hello.txt` with `System.Net.Http.HttpClient` (a smoke test only; upstream
      curl is BL-020's job), gets the file's bytes, then cancels and gets the
      normal-stop code.
- [ ] `SurlExitCode.FailedInit`'s XML doc no longer mentions the Phase 0 placeholder.
      `Surl.Console/CLAUDE.md` no longer calls the project a placeholder.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.

## Notes

## Log

- 2026-09-28: Created.
