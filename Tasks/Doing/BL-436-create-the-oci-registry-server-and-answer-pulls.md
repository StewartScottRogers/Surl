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
completed:
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

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - `/v2/` returns `200`;
      - a manifest by tag and by digest with the right `Content-Type` and `Docker-Content-Digest`;
      - `HEAD` returns the same headers and no body;
      - a blob by digest;
      - each unknown-item error with its code and status;
      - a tags list paged with `n` and `last` and a `Link` header;
      - referrers filtered by `artifactType`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
