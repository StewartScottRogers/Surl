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
completed:
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

- [ ] `Surl.Console` registers the TELNET server for `telnet`. A fast test in
      `Surl.Console.UnitTests` proves a `telnet://` listen URL starts a listener with it.
- [ ] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build for each case BL-035 recorded (plain session, `-t TTYPE=vt100`,
      `-t NEW_ENV=USER,alice`), with the recorded standard input, against a live `surl`,
      and assert exit code and stdout equal the recorded ones.
- [ ] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl` or
      `Surl.Conformance.UnitLibrary`.
- [ ] Any disagreement with the pinned build is fixed in `Surl.Protocol.Telnet` through a
      new task, never by changing the expected result. Such tasks are listed in the Log.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
