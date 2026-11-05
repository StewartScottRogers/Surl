---
id: BL-422
title: Run the Curl port's s3 client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-421, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/S3.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-422 — Run the Curl port's s3 client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl s3://`: a signed `GET`, a signed `PUT`
under `--allow-uploads`, a listing, and a refused unsigned request. Each test asserts what the S3 API
Reference says the exchange produces, and `Documentation/Conformance/Extended/S3.md` has no "covered by" entry
still pointing at a task.

## Context

- Rules: BL-543's ADR, decisions 1 to 3. The port's client is a live client here and nothing more. Each
  assertion is justified by the S3 API Reference, never by "what the port printed". A disagreement is
  investigated against the specification; a port defect is reported to Stewart in the task's Notes, never
  worked around in Surl.
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback` in `Conformance/Surl.Conformance.UnitTests`.
- Command lines: the port's `curl --extended-help s3`. The port's own cases in
  `Curl.Conformance.Extended.UnitTests/S3/` show its options (read with
  `git -C Z:\repos\Curl show origin/master:<path>`).
- Test class name: as BL-543's ADR decision 3 names it (planning name `CurlPortClientTransfersObjectsWithSurlOverS3Tests`).
  Every test is `[TestCategory("Integration")]` and inconclusive when the pinned client is absent.

## Acceptance criteria

- [ ] The test class exists with the four exchanges above. Each passes on Windows with the pinned port client
      present, and is inconclusive without it.
- [ ] `Documentation/Conformance/Extended/S3.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
