---
id: BL-615
title: Create Surl.Api and start and stop a Surl server in-process on its bound addresses
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-614]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
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

- [ ] `Application/Surl.Api` and `Application/Surl.Api.UnitTests` exist, are in `Surl.slnx`, and `Application/Surl.Api/CLAUDE.md`
      says what the project is, what it references and how it is tested.
- [ ] Named tests with a fake listener factory show: a start on `http://127.0.0.1:0` returning once bound with the
      fake's port in the bound URL; two listen URLs reporting two bound URLs in order; an `https` URL with no
      certificate throwing the ADR's exception with `SurlExitCode.CertificateProblem` and the text `surl` writes
      after `surl: `; stopping, then disposing, ending serving with no exception; disposing without stopping
      stopping it; a second server on the same data directory refused with exit code 124 while the first runs.
- [ ] A named test shows the defaults are the command line's: a start with only a listen URL composes the same
      `SurlCommandLine` as `surl <url>` with no other option.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean (AOT analysis included), the fast tests are green,
      and `Surl.Api` has 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
