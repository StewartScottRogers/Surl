---
id: BL-477
title: Run the Curl port's coap client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-476, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-477 — Run the Curl port's coap client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl coap://` and `coaps://`: a `GET`, a
block-wise `GET` of a large file, a `PUT` under `--allow-uploads`, an observation, and a DTLS handshake refused
for a bad identity. Each assertion comes from the RFCs, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help coap` and its cases in `Curl.Conformance.Extended.UnitTests/Coap/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the exchanges above, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/Coap.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
