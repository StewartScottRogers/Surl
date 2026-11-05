---
id: BL-615
title: Create Surl.Api and start and stop a Surl server in-process on its bound addresses
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-614]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Surl.slnx, Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-615 — Create Surl.Api and start and stop a Surl server in-process on its bound addresses

## Goal

A .NET test can write `await using var server = await SurlServer.StartAsync(options)` (or the names BL-611's ADR
decides) with a listen URL such as `http://127.0.0.1:0` or `ftp://127.0.0.1:0`, read the bound URL with its real
port, and stop the server, all in-process and with no `surl` process. A start `surl` would refuse throws with the
same `SurlExitCode` and text.

## Context

- Decision: BL-611's ADR, decisions 3, 4, 5, 6 and 8 (project, public types and members, failures, defaults, several
  servers per process). Use exactly the names it decides.
- Built on `Surl.Composition`'s start (BL-614): the options become a `SurlCommandLine` (`Application/Surl.Cli.UnitLibrary`)
  and go through the same start, so validation is identical. Production listeners are `Surl.Networking`'s
  `SocketListenerFactory`; tests pass a fake `IListenerFactory` through an internal seam
  (`Application/Surl.Console.UnitTests/FakeListenerFactory.cs` is a model) and open no socket.
- This task covers the listen URLs, content (the data directory, or the in-memory store with files the caller
  supplies, as the ADR decides), the start-up log writer, `TimeProvider`, and stop. Accounts, auth methods, TLS and
  limits are BL-616's; recording exchanges is the task after it.
- Project rules: `Application/Surl.Api` is a library and a naming exception (BL-610's ADR); `Directory.Build.props`
  makes it AOT-compatible; XML documentation on every public member (`.claude/rules/csharp-style.md`); the four
  quality gates in root `CLAUDE.md` apply to it as to `Surl.Console`. Add both projects to `Surl.slnx`'s
  `/Application/` folder in ordinal order.
- Port model, copyable but never evidence: `git -C Z:\repos\Curl show origin/master:Curl.Api/CLAUDE.md`.

## Acceptance criteria

- [x] `Application/Surl.Api` and `Application/Surl.Api.UnitTests` exist, are in `Surl.slnx`, and `Application/Surl.Api/CLAUDE.md`
      says what the project is, what it references and how it is tested.
- [x] Named tests with a fake listener factory show: a start on `http://127.0.0.1:0` returning once bound with the
      fake's port in the bound URL; two listen URLs reporting two bound URLs in order; an `https` URL with no
      certificate throwing the ADR's exception with `SurlExitCode.CertificateProblem` and the text `surl` writes
      after `surl: `; stopping, then disposing, ending serving with no exception; disposing without stopping
      stopping it; a second server on the same data directory refused with exit code 124 while the first runs.
- [x] A named test shows the defaults are the command line's: a start with only a listen URL composes the same
      `SurlCommandLine` as `surl <url>` with no other option.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean (AOT analysis included), the fast tests are green,
      and `Surl.Api` has 100% line and branch coverage.

## Notes

- Built to ADR-0099 decisions 3 to 8, names as decided: `SurlServer` (`StartAsync`, `BoundUrls`, `BoundUrl`,
  `StopAsync`, `DisposeAsync`), `SurlServerOptions` (listen URLs, `DataDirectory`, `Files`, the four content flags,
  `AdditionalArguments`, `Log`, `LogLevel`, `TimeProvider`, `AreExtensionsTurnedOn`) and `SurlServerStartException`.
  Accounts, TLS, host keys and limits stay BL-616's; `Exchanges` and recording stay BL-617's.
- Choices with a sensible default: `LogLevel` is spelled `--log-level <word>` only when it is not `info`, so the
  defaults compose exactly `surl <url>`; `LogLevel.Trace` writes the trace dump to `Log`, as `surl` writes it to the
  log stream when no trace file is given. `Log` is wrapped in `TextWriter.Synchronized`, since listeners write from
  several threads. Host-owned options are detected from the parse (outcome help/version/manual, `LogFile` or
  `TraceFile` set) rather than by scanning words, so every spelling (`-h`, `-V`, `-M`) is caught.
- `Files` paths: '/' separated, no empty, `.` or `..` segment, no `\` or `:`, and no file another path is inside
  (that would fail later in the in-memory file system with an `IOException`); each is an `ArgumentException`.
- The in-memory file system is always composed and handed over as `ContentWithoutDataDirectory`; with
  `DataDirectory` it is unused (empty).
- The tests use an internal seam, `SurlServerHostSeams` (listener factory, probe, lock, environment), with
  `ApiTestSeams`: a fake listener factory, an in-memory lock table and an in-memory data directory - no socket,
  no disk.
- Added `Measure-CodeQuality.ps1` to `touches` (no task in Doing names it): its production-assembly filter only
  knew `*.UnitLibrary` and `Surl.Console`, so it now measures `Surl.Api` too. Measured: `Surl.Api` 100% line,
  100% branch, 28 members, worst CRAP 10.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Api starts a Surl server in-process on its bound URLs, refuses as surl does, and stops; 100% covered
