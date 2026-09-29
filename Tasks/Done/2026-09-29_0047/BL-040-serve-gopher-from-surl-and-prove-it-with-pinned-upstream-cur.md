---
id: BL-040
title: Serve gopher from surl and prove it with pinned upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-034, BL-020]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-040 — Serve gopher from surl and prove it with pinned upstream curl

## Goal

`surl gopher://127.0.0.1:<port>/` serves the served directory over Gopher with the server
from BL-034, and integration tests prove that the pinned upstream curl 8.21.0 build gets
the files, menus and exit codes BL-034's recordings predict.

## Context

- BL-034's Gopher server and its fixtures in `Surl.Protocol.Gopher.UnitTests/Fixtures/`
  hold each case's command line and expected result.
- Register the server for `gopher` in `Surl.Console`'s explicit composition, with the
  same content store BL-019 wires for `http`.
- BL-020's process runner, in-process `surl` start and `Assert.Inconclusive` rule are
  reused. Each test serves a fresh temporary directory.

## Acceptance criteria

- [x] `Surl.Console` registers the Gopher server for `gopher`. A fast test in
      `Surl.Console.UnitTests` proves a `gopher://` listen URL starts a listener with it.
- [x] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build for a file selector, the root menu and a missing selector, against a live
      `surl`, and assert exit code and stdout as BL-034's recordings predict.
- [x] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.
- [x] Any disagreement with the pinned build is fixed in `Surl.Protocol.Gopher` through a
      new task, never by changing the expected result. Such tasks are listed in the Log.

## Notes

- `CommandLineRunner.ComposeProtocolServers` now builds `GopherProtocolServer` beside
  `HttpProtocolServer`, both over the one content store, so `surl --version` lists
  `Protocols: gopher http`. `CommandLineRunnerTests.RunAsync_GopherListenUrl_StartsAGopherListenerAndReturnsOkWhenCancelled`
  proves a `gopher://` listen URL starts a listener instead of the `(1)` refusal.
- `UpstreamCurlFetchesFromSurlOverGopherTests` runs the pinned win-x64 8.21.0 build with
  `-sS` against a live in-process `surl`: `0/file.txt` gives the file's bytes, the root
  gives the `root-menu` recording's menu with the bound port in place of 18634, and
  `0/missing.txt` gives the `missing-selector` error menu; all exit 0 with empty stderr.
  All three agreed with BL-034's recordings on the first run, so no Gopher fix task was filed.
- Choice: the root-menu test starts `surl` with `--list-directories`. Directory listings are
  off unless `--list-directories` is given, and without it the root answers with the error menu, which is
  the intended default and not a disagreement with upstream curl.
- Choice: `SurlOnLoopback.StartAsync` gained an overload taking the scheme, subdirectories
  to create and extra options, and the pinned-build lookup moved from the HTTP test class
  into a shared `PinnedUpstreamCurl` helper so both classes run curl the same way.
- `dotnet format --verify-no-changes` over the whole solution reports ENDOFLINE errors in
  `Surl.Networking.UnitTests/TestCertificates.cs`, outside this task's touches; the three
  projects this task changed verify clean.
- Verified 2026-09-29: `dotnet build -warnaserror` clean; fast tests green (Console 34,
  Conformance 60); `dotnet test Surl.Conformance.UnitTests --filter "FullyQualifiedName~Surl.Conformance"`
  70/70 with the pinned build; `Measure-CodeQuality.ps1 -Library Surl.Console` reports
  0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl serves gopher://; pinned upstream curl 8.21.0 gets the file, root menu and error menu BL-034 recorded
