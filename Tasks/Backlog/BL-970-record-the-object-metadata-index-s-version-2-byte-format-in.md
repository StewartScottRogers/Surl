---
id: BL-970
title: Record the object metadata index's version 2 byte format in ADR-0100
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-736]
touches: [Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-970 — Record the object metadata index's version 2 byte format in ADR-0100

## Goal

ADR-0100 decision 2.3.1 describes the object metadata index's byte format as the code writes it since BL-736: header `SURL-OBJECT-INDEX-2`, six header fields per entry with `Content-MD5` the sixth, and version 1 indexes still read.

## Context

- BL-736 added `Content-MD5` (Azure's unchecked `x-ms-blob-content-md5`, ADR-0109 decision 5.6) to `ObjectMetadata`, `ObjectProperties` and the index. `ObjectMetadataIndexFormat` now writes `SURL-OBJECT-INDEX-2` and LF, and entries carry `Content-Type`, `Content-Encoding`, `Content-Disposition`, `Cache-Control`, `Content-Language` and `Content-MD5` (six strings, empty absent). An index starting `SURL-OBJECT-INDEX-1` is still read, five fields per entry and no `Content-MD5`; the next write rewrites it as version 2. `Content-MD5` counts toward `MaxMetadataBytes` as the other header fields do.
- The code and pinned bytes: `Foundation/Surl.Content.UnitLibrary/ObjectMetadataIndexFormat.cs` remarks, `ObjectMetadataIndexTests.StoreAsync_PersistingIndex_WritesThePinnedBytes` and `LoadAsync_IndexWrittenBeforeContentMd5WasKept_ReadsItsFiveHeaderFieldsAndNoContentMd5`; the library's `CLAUDE.md` already says so.
- BL-736 could not edit the ADR: `Documentation/Planning/Decisions` was in BL-718's `touches` while it ran.

## Acceptance criteria

- [ ] ADR-0100 decision 2.3.1's byte format names header `SURL-OBJECT-INDEX-2`, six header fields with `Content-MD5` sixth, and states that a `SURL-OBJECT-INDEX-1` index is read with five fields and no `Content-MD5` - as an amendment dated and marked "Decided by Claude under Stewart's delegation", citing BL-736.
- [ ] Its "What counts toward `MaxMetadataBytes`" choice names six header fields.

## Notes

## Log

- 2026-11-06: Created.
