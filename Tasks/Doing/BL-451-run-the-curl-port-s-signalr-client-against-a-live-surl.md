---
id: BL-451
title: Run the Curl port's signalr client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-450, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-451 — Run the Curl port's signalr client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl signalr://` and `signalrs://`: an echo
invocation, a failing invocation, and a refused unauthenticated negotiate. Each assertion comes from
HubProtocol.md and TransportProtocols.md, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help signalr` and its cases in `Curl.Conformance.Extended.UnitTests/SignalR/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the three exchanges, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/SignalR.md`: every row is covered by a named test or marked `n/a` with a
      reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-12: Backlog -> Doing.
