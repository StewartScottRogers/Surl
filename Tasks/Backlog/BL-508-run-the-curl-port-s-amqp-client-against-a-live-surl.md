---
id: BL-508
title: Run the Curl port's amqp client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-507, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-508 — Run the Curl port's amqp client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl amqp://` and `amqps://`: publish a
message, get it back, get from an empty queue, and a refused bad login. Each assertion comes from AMQP 0-9-1, and
the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help amqp` and its cases in `Curl.Conformance.Extended.UnitTests/Amqp0/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/Amqp0.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
