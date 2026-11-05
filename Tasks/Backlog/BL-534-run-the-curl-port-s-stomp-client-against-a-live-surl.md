---
id: BL-534
title: Run the Curl port's stomp client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-533, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Stomp.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-534 — Run the Curl port's stomp client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl stomp://` and `stomps://`: send to a
destination, subscribe and receive, and a refused bad login. Each assertion comes from STOMP 1.2, and the matrix
is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help stomp` and its cases in `Curl.Conformance.Extended.UnitTests/Stomp/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the three exchanges, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/Stomp.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
