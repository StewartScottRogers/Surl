---
id: BL-523
title: Run the Curl port's kafka client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-973, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-523 — Run the Curl port's kafka client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl kafka://` and `kafkas://`: produce a
record with a `PLAIN` login, consume it from offset 0, consume a compressed batch, and a refused bad password.
Each assertion comes from the Kafka protocol guide, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help kafka` and its cases in `Curl.Conformance.Extended.UnitTests/Kafka/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/Kafka.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-522 replaced by BL-973 (BL-522 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
