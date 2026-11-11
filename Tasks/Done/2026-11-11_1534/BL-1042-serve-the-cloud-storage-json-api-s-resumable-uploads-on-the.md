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
completed: 2026-11-11
---
# BL-1042 — Serve the Cloud Storage JSON API's resumable uploads on the gs server

## Goal

The `gs` server answers `objects.insert` with `uploadType=resumable` on the JSON paths: opening, chunks, status check, completion and cancel.

## Context

- Decision: ADR-0148 decision 7 (resumable) and decision 8; chunk, status and completion rules are ADR-0110 amendment 2's, shared staging session, 1000 open uploads and 24-hour idle bound.
- Session URI is `/upload/storage/v1/b/<bucket>/o?uploadType=resumable&upload_id=<id>` as the request reached it; cancel is `499`.

## Acceptance criteria

- [x] The opening `POST` answers `200` with that `Location`, reading `X-Upload-Content-Type` and optional JSON metadata.
- [x] A chunk answers `308` with `Range`, a status check reports what is kept, and the last chunk answers `200` with `storage#object`.
- [x] Cancel answers `499`; a later request, an XML session ID on a JSON URL (and the reverse) and an expired session are `404 notFound`.
- [x] The 1001st open upload is `503 backendError`; an idle upload is discarded after 24 hours on the injected `TimeProvider`.
- [x] A malformed `Content-Range` or wrong chunk length is `400 badRequest`; a total past `--max-filesize` is `413`.
- [x] 100% line and branch coverage, complexity at most 10; build and fast tests green.

## Notes

- Built `GcsJsonResumableUpload` (open, chunk or status check, cancel), a `GcsUploadKind.JsonResumable` session kind found by ID and bucket (`GcsStagedUploads.FindJson`), and `GcsStagedUpload.Metadata` so `GcsUploadStaging.CommitAsync` stores the JSON opening's metadata. Route operations `ResumeUpload` and `CancelUpload` added; all three are method id `storage.objects.insert`.
- Choices recorded as ADR-0148 Amendment 2: conditions judged at the opening only; the metadata's `md5Hash` and `crc32c` are not checked at completion; `uploadType` on a `PUT` or `DELETE` is not judged; a status check whose total equals the bytes kept completes the upload; cancel is `499 Client Closed Request` with no body.
- Measured: library 100% line, 100% branch (Measure-CodeQuality.ps1 on this library); CA1502 clean at build; 679 tests pass in the project. Matrix `GoogleCloudStorage-Json.md` rows filled.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. gs server serves JSON resumable uploads: open, 308 chunks, status check, 200 completion, 499 cancel, 404 for foreign or expired sessions, 503 at 1001 open
