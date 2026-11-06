---
id: BL-728
title: List folder objects put as x/ in S3 ListObjectsV2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-419]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-728 — List folder objects put as x/ in S3 ListObjectsV2

## Goal

A folder created by a zero-byte PutObject of a key ending in `/` is listed by ListObjectsV2 as key `x/` with size 0, as ADR-0108 decision 6s last bullet says, while an empty folder made any other way stays unlisted.

## Context

- ADR-0108 decisions 3 and 6. BL-419 creates the folder on such a PUT but records nothing, so ListObjectsV2 lists no folder object: `ObjectMetadataIndex.StoreAsync` keys files only.
- Needs a way to remember which folders were put as objects - the index in `Surl.Content` (then add it to touches) or a marker the S3 library keeps; decide and record it.

## Acceptance criteria

- [x] A named test shows `PUT /bucket/x/` (empty body) then `GET /bucket?list-type=2` listing `<Key>x/</Key>` with `<Size>0</Size>` and the empty-body ETag `"d41d8cd98f00b204e9800998ecf8427e"`.
- [x] A named test shows a folder made by another protocol is not listed.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line and branch coverage.

## Notes

- Decision (ADR-0108 Amendment 3, decided by Claude under Stewart's delegation): folder objects
  live in `ObjectMetadataIndex` (`Surl.Content`), not in a marker the S3 library keeps. The index
  is what store snapshots, reset/restore and data-directory persistence already carry, and a
  dot-file marker would make the folder non-empty so other protocols could not remove it. New
  members: `StoreFolderObjectAsync`, `ReadFolderObjectsAsync`, `IsValidFolderObjectKey`; key
  `bucket/x/`; the index file format admits folder keys. A folder object lasts while its folder
  exists (a folder's last write time moves with its contents, so no stamp); stated cost: a folder
  removed and remade by another protocol before any listing looks is listed again.
- Touches widened to `Foundation/Surl.Content.UnitLibrary`, `Foundation/Surl.Content.UnitTests`
  (the index) and ADR-0108 (Amendment 3); no task in Doing named any of them when checked.
- ListObjectsV2 reads every folder object once per listing and `S3BucketWalk` yields `x/` just
  before the keys inside it, so paging and common prefixes count only real keys.
- A folder put as `x/` in a share has no index key: created, `200`, not listed. Index full: `503`.
- Follow-up filed: BL-928 (DeleteObject of a non-empty `x/` should forget the folder object).
- Tests: `S3ListObjectsV2Tests.ServeAsync_FolderPutAsAnObject_IsListedAsItsKeyWithSizeZeroAndTheEmptyETag`
  and `ServeAsync_FolderMadeByAnotherProtocol_IsNotListed`, plus prefix/delimiter, removed-folder
  and share cases; two PutObject cases; seven `ObjectMetadataIndexTests` cases. S3 314 and Content
  710 tests green; both libraries at 100% line and branch coverage (MSTest's cobertura collector).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. S3 ListObjectsV2 lists a folder put as x/ as key x/ (size 0, empty-body ETag), recorded as a folder object in the object metadata index; folders made otherwise stay unlisted
