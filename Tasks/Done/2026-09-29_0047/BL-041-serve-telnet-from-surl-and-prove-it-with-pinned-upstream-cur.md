---
id: BL-041
title: Serve telnet from surl and prove it with pinned upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-035, BL-020]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-041 — Serve telnet from surl and prove it with pinned upstream curl

## Goal

`surl telnet://127.0.0.1:<port>/` serves TELNET sessions with the server from BL-035, and
integration tests prove that the pinned upstream curl 8.21.0 build completes the
negotiation and session BL-035's recordings predict.

## Context

- BL-035's TELNET server and its fixtures in `Surl.Protocol.Telnet.UnitTests/Fixtures/`
  hold each case's command line, standard input and expected result.
- Register the server for `telnet` in `Surl.Console`'s explicit composition.
- BL-020's process runner is reused. If it cannot yet feed curl standard input, extend
  it in `Surl.Conformance.UnitLibrary` (in this task's `touches`), with fast tests.

## Acceptance criteria

- [x] `Surl.Console` registers the TELNET server for `telnet`. A fast test in
      `Surl.Console.UnitTests` proves a `telnet://` listen URL starts a listener with it.
- [x] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build for each case BL-035 recorded (plain session, `-t TTYPE=vt100`,
      `-t NEW_ENV=USER,alice`), with the recorded standard input, against a live `surl`,
      and assert exit code and stdout equal the recorded ones.
- [x] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl` or
      `Surl.Conformance.UnitLibrary`.
- [x] Any disagreement with the pinned build is fixed in `Surl.Protocol.Telnet` through a
      new task, never by changing the expected result. Such tasks are listed in the Log.

## Notes

- `CommandLineRunner.ComposeProtocolServers` now adds `TelnetProtocolServer` (it takes no
  content store). `surl --version` lists `Protocols: gopher http telnet`.
  `RunAsync_TelnetListenUrl_StartsATelnetListenerAndReturnsOkWhenCancelled` covers the
  listen URL.
- `UpstreamCurlRunner.RunAsync` gained an overload taking standard input bytes: it writes
  them all at once and closes stdin, exactly as `Record-CurlExchange.ps1` fed curl when
  BL-035 recorded, so the live run sees the same input. The old overload delegates with
  none. Both are process-only and `[ExcludeFromCodeCoverage]` like the original; the fast
  test `CreateStartInfo_Arguments_...` already pins `RedirectStandardInput`, so no further
  fast test was possible without starting a process.
- `UpstreamCurlTalksToSurlOverTelnetTests` runs all three recorded cases (the task's plain
  and `-t` sessions - recorded as one `telnet-options-session` with both `-t` options - and
  `iac-in-data`). Choice: each test reads `exitcode.txt` and `stdout.bin` straight from
  `Surl.Protocol.Telnet.UnitTests/Fixtures/<case>/` (read only, not in `touches`), so the
  expected result is the recording itself and cannot drift from it. The curl URL has no
  trailing slash, as recorded.
- Result on Windows with the pinned win-x64 build: all three pass, 5 of 5 repeat runs; no
  disagreement with the pinned build, so no fix task was filed.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl serves telnet:// and pinned upstream curl 8.21.0 completes all three recorded TELNET sessions against it
