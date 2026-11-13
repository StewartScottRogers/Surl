---
id: BL-422
title: Run the Curl port's s3 client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-421, BL-406, BL-1035]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/S3.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
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

- [x] The test class exists with the four exchanges above. Each passes on Windows with the pinned port client
      present, and is inconclusive without it.
- [x] `Documentation/Conformance/Extended/S3.md`: every row is covered by a named test or marked `n/a` with a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- 2026-11-11 (lane 4): `dotnet build` cannot start - `global.json` pins SDK 10.0.401 and this machine
  has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100. Returned to Backlog behind BL-1035. Found so far,
  for the next run: the pinned port client is installed at
  `%LOCALAPPDATA%\Surl\curl-port\b42c319b…\curl.exe`; its `s3` help says it signs with `--user
  ACCESS:SECRET`, reaches a non-AWS service through `AWS_ENDPOINT_URL`, lists for an empty key or one
  ending in `/`, and uploads with `-T`. `PinnedCurlPortClientOnThisMachine.RunAsync` passes no
  environment changes, so the test class needs an overload taking `AWS_ENDPOINT_URL`.

- 2026-11-12 (lane 2): BL-1035 resolved (SDK 10.0.404 installed). Added `CurlPortClientTransfersObjectsWithSurlOverS3Tests` (signed GET, signed PUT, list, unsigned refused; all pass on Windows with the pinned port client). Listing needs `--list-directories` on surl (S3ListObjectsV2 is 403 AccessDenied without it, as documented), so the test passes it. The unsigned test asserts a non-zero exit and no object bytes: the port reports a refusal as exit 22. Every `S3.md` row now names tests or is `n/a`; rows previously pointing at finished tasks were mapped to their unit tests. No port defect found.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. The Curl port's s3 client does a signed GET, signed PUT, listing and refused unsigned request against a live surl; S3.md rows all name tests
