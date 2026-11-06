---
id: BL-640
title: Prove with pinned upstream curl that an MQTT message published on one listener is read on another
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-635]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-640 — Prove with pinned upstream curl that an MQTT message published on one listener is read on another

## Goal

A conformance test class shows, with pinned upstream curl 8.21.0, that a message published with `curl -d … mqtt://`
on one listener is received by `curl mqtt(s)://…` on another listener of the same `surl`, through the shared
message bus, in memory and across a restart with `--directory`.

## Context

- Decision: BL-622's ADR, decision 5 (planning class `UpstreamCurlSharesTopicsAcrossSurlListenersTests` in
  `Conformance/Surl.Conformance.UnitTests`). BL-635 put the MQTT server on the bus.
- Cases: publish on `mqtt://127.0.0.1:<p1>/a/b` and subscribe on `mqtt://127.0.0.1:<p2>/a/b`; publish on `mqtt`,
  subscribe on `mqtts` (`--self-signed`, curl `-k`); publish, restart `surl` on the same `--directory`, subscribe;
  a data directory written before BL-635 (a `retained-messages` file in ADR-0031 decision 6's format) still serves
  its message. ADR-0014's one-shot subscribe decides what curl prints.
- Model: `UpstreamCurlTalksToSurlOverMqttTests.cs` (its recordings and `PublishRestartSubscribe_…` tests). Missing
  pinned build is Inconclusive; platform-neutral.

## Acceptance criteria

- [x] The class exists with a named test per case above, each asserting curl's exit code and the subscriber's
      stdout equal to the published payload as ADR-0014 frames it.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Class `UpstreamCurlSharesTopicsAcrossSurlListenersTests` (Integration, like its model), four tests, all four
  run and pass against pinned upstream curl 8.21.0 on Windows: two `mqtt` listeners in memory; `mqtt` publish to
  `mqtts` subscribe (`--self-signed`, curl `-k`); publish on listener 1, restart over `--directory`, subscribe on
  listener 2; an old `.surl/mqtt/retained-messages` (`SURL-MQTT-RETAINED-1\n` format) served on both listeners.
- Decision (sensible default): the topic is `t`, not `a/b`, so every expected exit code and stdout is a measured
  recording read from `Surl.Protocol.Mqtt.UnitTests/Fixtures` (`publish-hi`, `subscribe-t`, `mqtts-subscribe-t`)
  rather than bytes re-framed by hand for another topic (measure before pinning a byte). The topic name plays no
  part in what crosses listeners.
- Added `SurlOnLoopback.StartInMemoryAsync(schemes, options, ct)` (same project) to start several listeners
  without `--directory`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Pinned upstream curl publishes on one surl MQTT listener and reads it on another (mqtt, mqtts, after restart, from an old retained-messages file)
