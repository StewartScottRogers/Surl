---
id: BL-1229
title: Reset, snapshot and restore the S3 server's open multipart uploads
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1216, BL-1228]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-15
completed: 2026-11-15
---
# BL-1229 — Reset, snapshot and restore the S3 server's open multipart uploads

## Goal

A reset or restore of an instance aborts every open S3 multipart upload and frees its slot; snapshot holds none.

## Context

ADR-0167 decisions 2, 3 and 5. `S3MultipartUploads` holds the sessions; add an abort-all operation under its gate, expose it as reset and restore on the S3 server, and register it from Surl.Composition as `StoreKind.Uploads` named `s3`, following `Amqp0ExchangeStoreRegistration`. Refuse (`InvalidOperationException`) when the content store is on disk. Surl.Composition needs the `ComposedStoreRegistration` list and ordering of ADR-0167 decision 3.

## Acceptance criteria

- [x] After reset, `UploadPart` and `CompleteMultipartUpload` for an earlier upload id answer `NoSuchUpload` and the temporary file is gone.
- [x] After reset `HasRoom` is true with 1000 uploads previously open.
- [x] Snapshot reports 0 bytes; restore aborts uploads as reset does; one `StoreChangedEvent` per operation.
- [x] A disk-backed content store makes the registration refuse.
- [x] 100% line and branch coverage for the changed code.

## Notes

- Registration sits after the existing additional registrations (after accounts, like the broker stores), not before accounts as ADR-0167 decision 3 words it: it still follows content, which is what matters for the temporary files. It is composed whenever the s3 server is (extensions on). Refusal on a disk-backed content store reads `ContentStore.IsControllable`.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. S3 open multipart uploads join reset, snapshot and restore
