---
id: BL-434
title: Run the Curl port's gs client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-433, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-434 — Run the Curl port's gs client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl gs://`: a bearer `GET`, an HMAC-signed
`PUT` under `--allow-uploads`, a listing, and a refused unsigned request. Each assertion comes from the XML API
pages, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help gs` and its cases in `Curl.Conformance.Extended.UnitTests/GoogleCloudStorage/`
  (`STORAGE_EMULATOR_HOST` points the client at `surl`).
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/GoogleCloudStorage.md`: every row is covered by a named test or marked
      `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
