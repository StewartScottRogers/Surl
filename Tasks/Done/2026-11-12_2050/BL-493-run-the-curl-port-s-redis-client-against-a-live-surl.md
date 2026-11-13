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
completed: 2026-11-12
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

- [x] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/Redis.md`: every row is covered by a named test or marked `n/a` with a
      reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- The port's redis client always opens with `HELLO 3 AUTH`, so "the same over RESP3" is every exchange; the RESP3 null is checked by a GET of a missing key printing nothing. `redis` runs with `--allow-plaintext-auth`, `rediss` with `--self-signed`. Four exchanges, five test cases, all passing on Windows with the pinned client. No port defect seen. Redis.md rows now name the live tests.

## Log

- 2026-11-04: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. pinned Curl port redis client runs SET/GET with login, RESP3, SELECT and a refused password against live surl
