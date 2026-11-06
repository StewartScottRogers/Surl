---
id: BL-428
title: Run the Curl port's az and abfs clients against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-965, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/AzureStorage.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-428 — Run the Curl port's az and abfs clients against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl az://` and `surl abfss://`: a blob read,
write and listing, a Gen2 file create-append-flush and read, and a refused unauthenticated request. Each
assertion comes from the Azure REST API, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3. The port's client is only a live client. Assertions are justified
  by the REST API pages, and a port defect is reported in Notes, never worked around.
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. The port's command lines: its
  `curl --extended-help az` and its cases in `Curl.Conformance.Extended.UnitTests/AzureStorage/`.
- Every test is `[TestCategory("Integration")]` and inconclusive when the pinned client is absent.

## Acceptance criteria

- [ ] The test class exists with the exchanges above. Each passes on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/AzureStorage.md`: every row is covered by a named test or marked `n/a`
      with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-427 replaced by BL-965 (BL-427 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
