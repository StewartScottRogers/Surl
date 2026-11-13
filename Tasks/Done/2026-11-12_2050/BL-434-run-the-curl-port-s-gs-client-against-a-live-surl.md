---
id: BL-434
title: Run the Curl port's gs client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1026, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
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

- [x] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/GoogleCloudStorage.md`: every row is covered by a named test or marked
      `n/a` with a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- Surl has no `--gs-plaintext` yet (BL-1027), so the tests start `surl gs://` with `--self-signed` and point the port at it with `STORAGE_EMULATOR_HOST=https://127.0.0.1:<port>` plus `-k`; this works, so no port defect to report. Bearer is `--oauth2-bearer` against `--user :<token>`, HMAC is `--user id:secret`.
- The four exchanges pass (4/4, not skipped) with the pinned client present. The matrix rows were already named tests or `n/a`; the live tests were added to the GET, PUT, List and no-credential rows. Rows still reading BL-430 etc. belong to those tasks (V4 signing), not this one.

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-433 replaced by BL-1026 (BL-433 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. The pinned Curl port's gs client does a Bearer GET, HMAC PUT, listing and refused unsigned request against a live surl gs://
