---
id: BL-1042
title: Serve the Cloud Storage JSON API's resumable uploads on the gs server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1041]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage-Json.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-1042 — Serve the Cloud Storage JSON API's resumable uploads on the gs server

## Goal

The `gs` server answers `objects.insert` with `uploadType=resumable` on the JSON paths: opening, chunks, status check, completion and cancel.

## Context

- Decision: ADR-0148 decision 7 (resumable) and decision 8; chunk, status and completion rules are ADR-0110 amendment 2's, shared staging session, 1000 open uploads and 24-hour idle bound.
- Session URI is `/upload/storage/v1/b/<bucket>/o?uploadType=resumable&upload_id=<id>` as the request reached it; cancel is `499`.

## Acceptance criteria

- [ ] The opening `POST` answers `200` with that `Location`, reading `X-Upload-Content-Type` and optional JSON metadata.
- [ ] A chunk answers `308` with `Range`, a status check reports what is kept, and the last chunk answers `200` with `storage#object`.
- [ ] Cancel answers `499`; a later request, an XML session ID on a JSON URL (and the reverse) and an expired session are `404 notFound`.
- [ ] The 1001st open upload is `503 backendError`; an idle upload is discarded after 24 hours on the injected `TimeProvider`.
- [ ] A malformed `Content-Range` or wrong chunk length is `400 badRequest`; a total past `--max-filesize` is `413`.
- [ ] 100% line and branch coverage, complexity at most 10; build and fast tests green.

## Notes

## Log

- 2026-11-11: Created.
