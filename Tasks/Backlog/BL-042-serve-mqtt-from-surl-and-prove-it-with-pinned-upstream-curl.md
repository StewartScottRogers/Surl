---
id: BL-042
title: Serve mqtt from surl and prove it with pinned upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-036, BL-020]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-042 — Serve mqtt from surl and prove it with pinned upstream curl

## Goal

`surl mqtt://127.0.0.1:<port>/` serves MQTT with the server from BL-036, and integration
tests prove that the pinned upstream curl 8.21.0 build publishes to it and fetches from
it as BL-036's recordings predict.

## Context

- BL-036's MQTT server and its fixtures in `Surl.Protocol.Mqtt.UnitTests/Fixtures/` hold
  each case's command line and expected result, and the plan's rule for what a
  subscriber receives.
- Register the server for `mqtt` in `Surl.Console`'s explicit composition.
- BL-020's process runner, in-process `surl` start and `Assert.Inconclusive` rule are
  reused.

## Acceptance criteria

- [ ] `Surl.Console` registers the MQTT server for `mqtt`. A fast test in
      `Surl.Console.UnitTests` proves an `mqtt://` listen URL starts a listener with it.
- [ ] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build to publish (`-d hi mqtt://127.0.0.1:<P>/t`) and then to subscribe
      (`mqtt://127.0.0.1:<P>/t`) against one live `surl`. Both exit 0, and the
      subscribe's stdout is what BL-036's rule says it receives.
- [ ] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.
- [ ] Any disagreement with the pinned build is fixed in `Surl.Protocol.Mqtt` through a
      new task, never by changing the expected result. Such tasks are listed in the Log.

## Notes

## Log

- 2026-09-28: Created.
