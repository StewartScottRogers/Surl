---
id: BL-440
title: Run the Curl port's oci client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1028, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/OciRegistry.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-440 — Run the Curl port's oci client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl oci://`: a blob push and a manifest push
under `--allow-uploads` with a token login, a pull of both by tag and digest, and a refused unauthenticated push.
Each assertion comes from the distribution spec, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help oci` and its cases in `Curl.Conformance.Extended.UnitTests/OciRegistry/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [x] The test class exists with the exchanges above, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/OciRegistry.md`: every row is covered by a named test or marked `n/a`
      with a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- Test class `CurlPortClientTransfersContentWithSurlOverOciTests` (6 Integration tests, all pass with the pinned port client): blob push and manifest push with `-u` token login under `--allow-uploads`, manifest pull by tag and digest, blob pull by digest, unauthenticated push refused. Expected values come from the distribution spec (stored bytes under their digest, `index.json` tag listing), not from port output. No port defect found.
- Matrix: rows this client exercises now name the test beside their owner task; rows whose proof is a server-side unit test owned by BL-436/BL-437 keep those IDs (the client cannot exercise chunked upload, mounts, referrers, deletes or limits), so they are not this task's to mark.
- Seeding writes an OCI Image Layout straight into the served directory (ADR-0115 decision 2.1).

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-439 replaced by BL-1028 (BL-439 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. the Curl port's oci client pushes and pulls blobs and manifests against a live surl oci:// with token login, and an unauthenticated push is refused
