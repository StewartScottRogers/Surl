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
completed: 2026-11-15
---
# BL-1174 — Measure pinned upstream curl 8.21.0 against the proxy's delay, drop and chosen-error faults in a stand-in recipe

## Goal

A stand-in recipe proves, with pinned upstream curl 8.21.0, what a test sees when the Surl proxy is told to fail: the exit code and stderr for a chosen `CONNECT` error, a chosen forwarded-request error, a command-stage drop, and a delay longer than curl's `--max-time`.

## Context

- Upstream curl is the only oracle (ADR-0003); measure only with builds pinned in `UpstreamCurlBuilds.json`, through the recipe harness the existing `StandIn*RecipeTests` in `Conformance/Surl.Conformance.UnitTests` use (`StandInFaultRecipeTests.cs` is the closest model). Never the Curl port.
- BL-1153's ADR names the exit codes it expects from upstream's documentation (https://curl.se/libcurl/c/libcurl-errors.html: for example 56 for a `CONNECT` answered non-2xx, 22 with `--fail` for a forwarded 5xx, 28 for `--max-time`); this task measures them. Where a measurement differs, the measurement wins and the ADR gets an amendment line saying so.
- Where upstream's answer differs by platform (a reset seen as 56, 55 or 7, as the wiki records for `DropConnection`), accept each measured value or pin per platform with `[OSCondition]`.

## Acceptance criteria

- [x] A new class `StandInProxyFaultRecipeTests` in `Conformance/Surl.Conformance.UnitTests`, `[TestCategory("Integration")]`, starts `SurlServer` with an HTTP origin and a proxy listener on port 0 and runs pinned upstream curl 8.21.0 with `-x` (and `-p` for `CONNECT`).
- [x] It pins the measured exit code and the measured stderr line for: a chosen `CONNECT` error, a chosen forwarded-request error (with `--fail`), a command-stage drop, and a delay over `--max-time 1`.
- [x] It shows a `times=1` rule fails only the first curl run and the retry succeeds.
- [x] Any difference from BL-1153's expected exit codes is recorded as an amendment line in that ADR.

## Notes

- `Surl.Api` cannot start a proxy-only server (it needs a `ListenUrls` entry and maps bound URLs to them), so the recipe runs the origin through `SurlServer` and the proxy as a `surl --proxy` process through a new `SurlOnLoopback.StartHttpProxyInMemoryAsync`.
- Measured: `CONNECT` 503 gives exit 7, not 56; recorded as ADR-0160 amendment 1. Forwarded 503 with --fail 22; drop 56 (Windows); delay over --max-time 1 gives 28.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Pinned curl 8.21.0 measured against the proxy's chosen-error, drop, delay and times=1 faults; ADR-0160 amended (CONNECT error is exit 7)
