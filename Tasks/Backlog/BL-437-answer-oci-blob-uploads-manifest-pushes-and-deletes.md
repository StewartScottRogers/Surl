---
id: BL-437
title: Answer OCI blob uploads, manifest pushes and deletes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-436]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-437 — Answer OCI blob uploads, manifest pushes and deletes

## Goal

The OCI registry accepts blob uploads in every way the spec defines: POST then PUT, a single POST with
`?digest=`, chunked PATCH with `Content-Range`, and cross-repository mount. It accepts manifest pushes by tag
and digest (with the `OCI-Subject` header for a manifest with a subject), and deletes if BL-435's ADR
serves them. It checks every digest and refuses mismatches with `DIGEST_INVALID`.

## Context

- Decision: BL-435's ADR, decisions 2, 3 and 7. Pushes only under `--allow-uploads`.
- Distribution spec v1.1.0, "Push": `202 Accepted` with `Location` for an upload session, `Range: 0-<n>` on
  PATCH, `416` for an out-of-order chunk, `201 Created` with `Location` and `Docker-Content-Digest` on completion,
  and `MANIFEST_BLOB_UNKNOWN` for a manifest naming a missing blob.
- Upload sessions expire after the ADR's lifetime, on an injected `TimeProvider`.

## Acceptance criteria

- [ ] Named tests show:
      - each of the four upload ways completing, with the stored blob readable by digest;
      - a chunk out of order gets `416`;
      - a wrong digest gets `DIGEST_INVALID`;
      - a manifest naming a missing blob gets `MANIFEST_BLOB_UNKNOWN`;
      - a manifest with a subject is listed by the referrers API and answered with `OCI-Subject`;
      - an expired upload session gets `BLOB_UPLOAD_UNKNOWN`;
      - a push refused when uploads are off.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
