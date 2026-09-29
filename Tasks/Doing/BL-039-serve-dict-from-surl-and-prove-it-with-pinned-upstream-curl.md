---
id: BL-039
title: Serve dict from surl and prove it with pinned upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-033, BL-020]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-039 — Serve dict from surl and prove it with pinned upstream curl

## Goal

`surl dict://127.0.0.1:<port>/` serves DICT with the server from BL-033, and integration
tests prove that the pinned upstream curl 8.21.0 build gets the replies and exit codes
BL-033's recordings predict.

## Context

- BL-033's DICT server and its fixtures in `Surl.Protocol.Dict.UnitTests/Fixtures/`
  hold each case's command line and expected result.
- Register the server for `dict` in `Surl.Console`'s explicit composition, as BL-019 did
  for `http`, and add a `ProjectReference` from `Surl.Console` only if it is missing.
  The csproj already references every protocol server.
- BL-020's process runner, in-process `surl` start and `Assert.Inconclusive` rule are
  reused.

## Acceptance criteria

- [ ] `Surl.Console` registers the DICT server for `dict`. A fast test in
      `Surl.Console.UnitTests` proves a `dict://` listen URL starts a listener with it.
- [ ] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build for each case BL-033 recorded, against a live `surl`, and assert its exit
      code and stdout equal the recorded ones.
- [ ] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.
- [ ] Any disagreement with the pinned build is fixed in `Surl.Protocol.Dict` through a
      new task, never by changing the expected result. Such tasks are listed in the Log.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
