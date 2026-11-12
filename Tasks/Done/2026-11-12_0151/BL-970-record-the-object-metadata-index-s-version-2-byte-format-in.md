---
id: BL-970
title: Record the object metadata index's version 2 byte format in ADR-0100
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-736, BL-1035]
touches: [Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-970 — Record the object metadata index's version 2 byte format in ADR-0100

## Goal

ADR-0100 decision 2.3.1 describes the object metadata index's byte format as the code writes it since BL-736: header `SURL-OBJECT-INDEX-2`, six header fields per entry with `Content-MD5` the sixth, and version 1 indexes still read.

## Context

- BL-736 added `Content-MD5` (Azure's unchecked `x-ms-blob-content-md5`, ADR-0109 decision 5.6) to `ObjectMetadata`, `ObjectProperties` and the index. `ObjectMetadataIndexFormat` now writes `SURL-OBJECT-INDEX-2` and LF, and entries carry `Content-Type`, `Content-Encoding`, `Content-Disposition`, `Cache-Control`, `Content-Language` and `Content-MD5` (six strings, empty absent). An index starting `SURL-OBJECT-INDEX-1` is still read, five fields per entry and no `Content-MD5`; the next write rewrites it as version 2. `Content-MD5` counts toward `MaxMetadataBytes` as the other header fields do.
- The code and pinned bytes: `Foundation/Surl.Content.UnitLibrary/ObjectMetadataIndexFormat.cs` remarks, `ObjectMetadataIndexTests.StoreAsync_PersistingIndex_WritesThePinnedBytes` and `LoadAsync_IndexWrittenBeforeContentMd5WasKept_ReadsItsFiveHeaderFieldsAndNoContentMd5`; the library's `CLAUDE.md` already says so.
- BL-736 could not edit the ADR: `Documentation/Planning/Decisions` was in BL-718's `touches` while it ran.

## Acceptance criteria

- [x] ADR-0100 decision 2.3.1's byte format names header `SURL-OBJECT-INDEX-2`, six header fields with `Content-MD5` sixth, and states that a `SURL-OBJECT-INDEX-1` index is read with five fields and no `Content-MD5` - as an amendment dated and marked "Decided by Claude under Stewart's delegation", citing BL-736.
- [x] Its "What counts toward `MaxMetadataBytes`" choice names six header fields.

## Notes

- Amendment written inline in decision 2.3.1 (byte format step 1 and the six-string bullet updated in place, with a dated "Amendment: version 2 keeps `Content-MD5`" paragraph after the format) plus a line in the ADR header's amendment list, so a reader of the format sees the current bytes first. Facts checked against `ObjectMetadataIndexFormat.cs` (`Header`, `HeaderWithoutContentMd5`, `headerFieldCount`). Lane 6's earlier attempt failed integration on a build break elsewhere, not in this docs-only diff; its ADR edit was reused unchanged. 2026-11-11 lane 5: the ADR edit is ready (on branch factory/BL-970-lane-6-20261111-060243), but `dotnet build` cannot run on this machine - `global.json` pins SDK 10.0.401, which is not installed (only 10.0.100-rc.1, 11.0.100-rc.2, 11.0.100). Added BL-1035 to `depends-on`; once it is Done, cherry-pick that branch, build, test and finish.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-970-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-970-lane-6-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; ADR edit is ready on factory/BL-970-lane-6-20261111-060243
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. ADR-0100 decision 2.3.1 records the object index version 2 byte format (six header fields, Content-MD5 sixth, version 1 still read)
