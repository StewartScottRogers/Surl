---
id: BL-1174
title: Measure pinned upstream curl 8.21.0 against the proxy's delay, drop and chosen-error faults in a stand-in recipe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1173]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1174 — Measure pinned upstream curl 8.21.0 against the proxy's delay, drop and chosen-error faults in a stand-in recipe

## Goal

A stand-in recipe proves, with pinned upstream curl 8.21.0, what a test sees when the Surl proxy is told to fail: the exit code and stderr for a chosen `CONNECT` error, a chosen forwarded-request error, a command-stage drop, and a delay longer than curl's `--max-time`.

## Context

- Upstream curl is the only oracle (ADR-0003); measure only with builds pinned in `UpstreamCurlBuilds.json`, through the recipe harness the existing `StandIn*RecipeTests` in `Conformance/Surl.Conformance.UnitTests` use (`StandInFaultRecipeTests.cs` is the closest model). Never the Curl port.
- BL-1153's ADR names the exit codes it expects from upstream's documentation (https://curl.se/libcurl/c/libcurl-errors.html: for example 56 for a `CONNECT` answered non-2xx, 22 with `--fail` for a forwarded 5xx, 28 for `--max-time`); this task measures them. Where a measurement differs, the measurement wins and the ADR gets an amendment line saying so.
- Where upstream's answer differs by platform (a reset seen as 56, 55 or 7, as the wiki records for `DropConnection`), accept each measured value or pin per platform with `[OSCondition]`.

## Acceptance criteria

- [ ] A new class `StandInProxyFaultRecipeTests` in `Conformance/Surl.Conformance.UnitTests`, `[TestCategory("Integration")]`, starts `SurlServer` with an HTTP origin and a proxy listener on port 0 and runs pinned upstream curl 8.21.0 with `-x` (and `-p` for `CONNECT`).
- [ ] It pins the measured exit code and the measured stderr line for: a chosen `CONNECT` error, a chosen forwarded-request error (with `--fail`), a command-stage drop, and a delay over `--max-time 1`.
- [ ] It shows a `times=1` rule fails only the first curl run and the retry succeeds.
- [ ] Any difference from BL-1153's expected exit codes is recorded as an amendment line in that ADR.

## Notes

## Log

- 2026-11-14: Created.
