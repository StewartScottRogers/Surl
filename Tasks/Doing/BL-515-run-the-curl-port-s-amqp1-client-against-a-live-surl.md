---
id: BL-515
title: Run the Curl port's amqp1 client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-514, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-515 — Run the Curl port's amqp1 client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl amqp1://` and `amqp1s://`: send a
message to an address, receive it, receive from an empty address, and a refused bad login. Each assertion comes
from AMQP 1.0, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help amqp1` and its cases in `Curl.Conformance.Extended.UnitTests/Amqp1/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/Amqp1.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-12: Backlog -> Doing.
