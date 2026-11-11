---
id: BL-737
title: Record BL-425's List Blobs and Put Blob choices in ADR-0109
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-425]
touches: [Documentation/Planning/Decisions/ADR-0109-how-the-azure-storage-server-answers-az-abfs-and-abfss.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-737 — Record BL-425's List Blobs and Put Blob choices in ADR-0109

## Goal

ADR-0109 records the choices BL-425 made where the ADR was silent, marked "Decided by Claude under Stewart's delegation".

## Context

- BL-425's Notes list the choices: List Blobs needs `--list-directories` (off `403 AuthorizationPermissionMismatch`), as ADR-0108 holds for S3's ListObjectsV2; a Put Blob body that is unreadable or cut short is `400 InvalidInput`; `OutOfRangeQueryParameterValue` carries `QueryParameterName`, `QueryParameterValue` and `MinimumAllowed`; `InvalidQueryParameterValue` on List Blobs carries a fixed `Reason` per parameter; List Blobs' `Etag` is written without its quotes, an absent property as an empty element, blobs and `BlobPrefix`es interleaved in name order, and `Metadata` as `<Metadata />` when a blob has none.
- The library's `CLAUDE.md` already states them; BL-425 could not edit `Documentation/Planning/Decisions` because BL-503 held it.

## Acceptance criteria

- [x] ADR-0109 decisions 3.1, 5.3 and 5.6 (or an amendment section) state each choice in BL-425's Notes, with why, marked "Decided by Claude under Stewart's delegation".
- [x] Every statement agrees with `Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary/CLAUDE.md`.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0109 has the BL-425 amendment recording its List Blobs and Put Blob choices, each checked against the library and its CLAUDE.md
