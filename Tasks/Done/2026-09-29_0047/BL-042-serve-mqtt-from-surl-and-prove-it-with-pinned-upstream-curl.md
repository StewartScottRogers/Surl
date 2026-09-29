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
completed: 2026-09-29
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

- [x] `Surl.Console` registers the MQTT server for `mqtt`. A fast test in
      `Surl.Console.UnitTests` proves an `mqtt://` listen URL starts a listener with it.
- [x] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build to publish (`-d hi mqtt://127.0.0.1:<P>/t`) and then to subscribe
      (`mqtt://127.0.0.1:<P>/t`) against one live `surl`. Both exit 0, and the
      subscribe's stdout is what BL-036's rule says it receives.
- [x] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.
- [x] Any disagreement with the pinned build is fixed in `Surl.Protocol.Mqtt` through a
      new task, never by changing the expected result. Such tasks are listed in the Log.

## Notes

- Plan: mirror BL-041 (TELNET). `CommandLineRunner.ComposeProtocolServers` adds
  `new MqttProtocolServer(new MqttRetainedMessages())` with the default limits; one
  `MqttRetainedMessages` per `surl` run, so a publish on one connection is what a later
  subscribe on another receives. `--version` now lists `gopher http mqtt telnet`.
- Fast test: `CommandLineRunnerTests.RunAsync_MqttListenUrl_StartsAnMqttListenerAndReturnsOkWhenCancelled`.
- Integration tests: `UpstreamCurlTalksToSurlOverMqttTests` publishes then subscribes to
  topic `t` on one live `surl`. Expected exit codes and stdout are read from BL-036's
  fixtures: `publish-hi` then `subscribe-t` (stdout `00 01 74 68 69`), and
  `publish-200-bytes` then `subscribe-200-bytes`. Choice: the second case also exercises
  the two-byte remaining length end to end; `subscribe-200-bytes` was recorded with 200
  `y`s retained, so the test swaps the payload bytes for the 200 `x`s published and leaves
  the recorded length and topic prefix as recorded.
- Result on Windows with the pinned 8.21.0 build: both pass; the whole
  `FullyQualifiedName~Surl.Conformance` run is 75/75 green. No disagreement with the
  pinned build, so no follow-up task was filed.
- `Measure-CodeQuality.ps1`: 0 failing members; `Surl.Console` 100% line and branch.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl serves mqtt; pinned upstream curl 8.21.0 publishes to it and a later subscribe receives the retained message as BL-036's recordings predict
