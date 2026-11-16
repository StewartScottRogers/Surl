---
id: BL-1231
title: Reset, snapshot and restore the Google Cloud Storage server's staged uploads
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1216, BL-1228]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-15
completed: 2026-11-15
---
# BL-1231 — Reset, snapshot and restore the Google Cloud Storage server's staged uploads

## Goal

A reset or restore of an instance aborts every open GCS resumable and XML multipart upload and frees its slot.

## Context

ADR-0167 decisions 2, 3 and 5. `GcsStagedUploads` holds the sessions; add an abort-all under its gate, expose reset and restore on the server, register from Surl.Composition as `StoreKind.Uploads` named `gcs`, following `Amqp0ExchangeStoreRegistration`; refuse when the content store is on disk.

## Acceptance criteria

- [x] After reset, a resumable upload PUT to an earlier session URI answers 404 and the temporary file is gone.
- [x] After reset `HasRoom` is true with 1000 uploads previously open.
- [x] Snapshot reports 0 bytes; restore aborts as reset does; one `StoreChangedEvent` per operation.
- [x] A disk-backed content store makes the registration refuse.
- [x] 100% line and branch coverage for the changed code.

## Notes

Named the event `gcs` per the task; the registration joins the one Uploads entry through `UploadStoreRegistrations.Combine` (order s3, azure, gcs). Existing S3 and Azure tests now expect the extra `gcs` events.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. GCS resumable and multipart uploads are aborted by instance reset and restore, registered as Uploads store gcs
