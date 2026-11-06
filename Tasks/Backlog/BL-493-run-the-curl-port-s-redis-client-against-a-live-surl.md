---
id: BL-493
title: Run the Curl port's redis client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-492, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Redis.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-493 — Run the Curl port's redis client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl redis://` and `rediss://`: `SET` then
`GET` with a login, the same over RESP3, a `SELECT`ed database from the URL, and a refused wrong password. Each
assertion comes from the RESP specification and the command pages, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help redis` and its cases in `Curl.Conformance.Extended.UnitTests/Redis/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/Redis.md`: every row is covered by a named test or marked `n/a` with a
      reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
