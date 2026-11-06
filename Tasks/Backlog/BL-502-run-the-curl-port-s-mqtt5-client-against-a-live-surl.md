---
id: BL-502
title: Run the Curl port's mqtt5 client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-975, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Mqtt5.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-502 — Run the Curl port's mqtt5 client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl mqtt5://`, `mqtt5s://` and `mqtt5+ws://`:
a QoS 1 publish, a subscribe that receives a retained message, a QoS 2 round trip, and a refused bad password.
Each assertion comes from MQTT 5.0, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help mqtt5` and its cases in `Curl.Conformance.Extended.UnitTests/Mqtt5/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.
- The port's subscribe reads until the server sends DISCONNECT, and the broker keeps subscribers by default
  (ADR-0093 decision 0), so the subscribe case starts `surl` with `--mqtt5-disconnect-after-retained`
  (decision 9).

## Acceptance criteria

- [ ] The test class exists with the exchanges above over each transport, each passing on Windows with the pinned
      client present.
- [ ] `Documentation/Conformance/Extended/Mqtt5.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-501 replaced by BL-975 (BL-501 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
