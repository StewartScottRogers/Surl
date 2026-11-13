---
id: BL-529
title: Run the Curl port's nats client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-528, BL-406, BL-1035]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Nats.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-529 — Run the Curl port's nats client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl nats://` and `natss://`: subscribe and
receive a publish, publish, request and reply, and a refused bad login. Each assertion comes from the NATS client
protocol, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help nats` and its cases in `Curl.Conformance.Extended.UnitTests/Nats/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/Nats.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- 2026-11-11: `dotnet` finds no compatible SDK on this machine: `global.json` pins 10.0.401 (rollForward latestFeature) and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed, so nothing can be built or tested. Added BL-1035 (reinstall the pinned SDK) to `depends-on` and returned the task to Backlog.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-12: Backlog -> Doing.
