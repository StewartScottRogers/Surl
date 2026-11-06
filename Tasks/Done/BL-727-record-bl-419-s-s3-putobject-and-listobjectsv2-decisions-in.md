---
id: BL-727
title: Record BL-419's S3 PutObject and ListObjectsV2 decisions in ADR-0108
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-419]
touches: [Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-727 — Record BL-419's S3 PutObject and ListObjectsV2 decisions in ADR-0108

## Goal

ADR-0108 records, as an amendment marked Decided by Claude under Stewart's delegation, the choices BL-419 made while building PutObject, ListObjectsV2 and virtual-hosted addressing.

## Context

- BL-419 could not edit the ADR folder (BL-463 held it), so its decisions sit in BL-419's Notes (Tasks/Done) and in `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/CLAUDE.md`.
- The choices: ListObjectsV2 needs `--list-directories` (off: 403 AccessDenied); `--allow-uploads` and `--max-filesize` are read from the store's `ContentExposureOptions`; the listen host comes from `ExchangeContext.ListenUrl`; `400 IncompleteBody` for a body that ends early; chunked PUT `400 InvalidRequest`; the metadata index keys a `--bucket` object under its content path and keeps no metadata for a share; a PutObject the index refuses (MetadataTooLarge, index full) removes the written file.

## Acceptance criteria

- [x] ADR-0108 has an amendment section listing each choice in Context with its reason, and its table of codes gains `IncompleteBody` (400).
- [x] Every statement in the amendment is true of `Surl.Extended.Protocol.S3.UnitLibrary` as it is.

## Notes

Done as ADR-0108 Amendment 2 (eight numbered choices, each with its reason) plus an `IncompleteBody` (400) row in decision 1.1's table. Each statement was checked against the library on 2026-11-06: `S3ListObjectsV2` (ListDirectories gate), `S3PutObject` (ExposureOptions.AllowUploads and MaxUploadBytes, BodyRefusal, BodyFramingRefusal, StoreMetadataAsync deleting the file on a refused index write), `S3RequestAnswerer` (ListenUrl.Host), `S3IndexKey`. Also recorded the folder-creation choice from BL-419's Notes (409 conflicts, `x/` not yet listed: BL-728) and that aws-chunked stays 501 until BL-711, since BL-711, BL-728 and BL-729 are still in Backlog. Written directly rather than through align-and-document: a single ADR section.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0108 Amendment 2 records BL-419's PutObject, ListObjectsV2 and virtual-hosting choices and lists IncompleteBody (400)
