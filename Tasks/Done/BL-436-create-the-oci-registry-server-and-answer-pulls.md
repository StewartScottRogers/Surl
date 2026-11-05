---
id: BL-436
title: Create the OCI registry server and answer pulls
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-435, BL-404, BL-407]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-436 — Create the OCI registry server and answer pulls

## Goal

`Surl.Extended.Protocol.OciRegistry.UnitLibrary` exists. It answers `GET /v2/`, manifest `GET` and `HEAD` by tag
or digest with content negotiation on `Accept`, blob `GET` and `HEAD` by digest, the tags list and the referrers
API, with the distribution spec's JSON error bodies.

## Context

- Decision: BL-435's ADR, decisions 1, 2 and 5. Rules: BL-543's ADR.
- New projects in `Protocols/FileTransfer/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.OciRegistry`.
  References: `Surl.Protocol.Abstractions`, `Surl.Content`, and BL-407's library.
- Distribution spec v1.1.0, "Pull" and "Content discovery": `Docker-Content-Digest`, `Content-Type` as the
  manifest's `mediaType`, `404 MANIFEST_UNKNOWN` / `BLOB_UNKNOWN` / `NAME_UNKNOWN`, tags list `?n=&last=` paging
  with a `Link` header, and the referrers response as an image index.
- Authentication is BL-438's. These tests run with anonymous pulls allowed.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - `/v2/` returns `200`;
      - a manifest by tag and by digest with the right `Content-Type` and `Docker-Content-Digest`;
      - `HEAD` returns the same headers and no body;
      - a blob by digest;
      - each unknown-item error with its code and status;
      - a tags list paged with `n` and `last` and a `Link` header;
      - referrers filtered by `artifactType`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- 2026-11-05: Built from ADR-0115 decisions 1, 2, 3 and 5 and the specifications' text; the Curl port was not
  read. 140 tests in `Surl.Extended.Protocol.OciRegistry.UnitTests`: `OciRegistryProtocolServerTests` (`/v2/`,
  `404`/`405 UNSUPPORTED`, `NAME_INVALID`, `NAME_UNKNOWN`, the `429` refusal), `OciPullAnswersTests` (manifests by
  tag and digest, `HEAD`, blobs with ranges, `MANIFEST_UNKNOWN`, `BLOB_UNKNOWN`), `OciTagsListTests` (`n`, `last`,
  `Link`), `OciReferrersTests` (`artifactType` and `OCI-Filters-Applied`). Coverage measured with
  `Measure-CodeQuality.ps1 -SkipTestRun` on the project's own Cobertura run: 100% line, 100% branch, CRAP at most 10.
- Defaults taken: every request is the anonymous caller on `oci` until BL-438 adds tokens (the server is not
  composed into `surl` until BL-439, so nothing is exposed meanwhile); the upload paths (`/blobs/uploads/…`) answer
  `404 UNSUPPORTED` and manifest/blob `PUT`/`DELETE` `405` with `Allow: GET, HEAD` until BL-437; `/v2/`, tag list
  and referrers allow `GET` only. Manifests are read up to 4 MiB (decision 7) and `index.json` up to 16 MiB; larger
  is unknown / read as empty and noted. A `HEAD` error carries the error body's `Content-Length` and no body.
- The name and tag grammars are hand-checked (`OciNames`), not `[GeneratedRegex]`: the generated matcher's code
  counts against the library's coverage and complexity gates.
- The digest check runs on every read; ADR-0115 decision 2.1's cache in `ObjectMetadataIndex` is filed as BL-865.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl's OCI registry library answers /v2/, manifest and blob pulls, the tag list and the referrers API with the spec's JSON errors
