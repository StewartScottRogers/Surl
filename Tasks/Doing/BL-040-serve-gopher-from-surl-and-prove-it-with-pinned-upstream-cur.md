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
completed:
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

- [ ] `Surl.Console` registers the Gopher server for `gopher`. A fast test in
      `Surl.Console.UnitTests` proves a `gopher://` listen URL starts a listener with it.
- [ ] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build for a file selector, the root menu and a missing selector, against a live
      `surl`, and assert exit code and stdout as BL-034's recordings predict.
- [ ] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.
- [ ] Any disagreement with the pinned build is fixed in `Surl.Protocol.Gopher` through a
      new task, never by changing the expected result. Such tasks are listed in the Log.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
