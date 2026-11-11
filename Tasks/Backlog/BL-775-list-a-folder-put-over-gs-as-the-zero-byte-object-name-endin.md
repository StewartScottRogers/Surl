---
id: BL-775
title: List a folder put over gs as the zero-byte object name ending in a slash
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-431, BL-1035]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-775 — List a folder put over gs as the zero-byte object name ending in a slash

## Goal

After a zero-byte `PUT /<bucket>/x/` on `gs`, List Objects lists `x/` with `Size` 0, as ADR-0110 decision 6
says ("Empty folders are not objects, unless a client put `x/`, which the index records and lists as `x/` with
size 0"), while a folder another protocol made stays unlisted.

## Context

- BL-431 creates the folder for such a PUT and answers `200` with the empty object's digests, but records
  nothing: `ObjectMetadataIndex` keys files only (`TryReadStamp` needs a file), and `Surl.Content` was outside
  BL-431's `touches`.
- The index needs a folder-marker entry (keyed `x/`, stamped by the folder's existence), and `GcsBucketWalk`
  needs to yield a marked folder as the name `x/` before the names inside it.
- Matrix row `GCS:Surl-Put-1` in `Documentation/Conformance/Extended/GoogleCloudStorage.md`.

## Acceptance criteria

- [ ] A named test shows `PUT /photos/x/` (zero bytes) then `GET /photos` listing `<Key>x/</Key>` with `<Size>0</Size>`.
- [ ] A named test shows a folder created through the content store directly is still not listed as an object.
- [ ] `dotnet build` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 4): No change to Surl.Content is needed - ObjectMetadataIndex already keeps folder objects (StoreFolderObjectAsync / ReadFolderObjectsAsync, BL-728). The GCS change mirrors S3: GcsIndexKey.OfFolderObject, GcsPutObject.StoreFolderAsync records the folder object (generation = upload time, index full 403 InsufficientQuota, share folders unrecorded), GcsBucketWalk yields x/ before the names inside it, GcsListObjects reads folder objects once. Code and tests (GcsListObjectsTests, GcsPutObjectTests) were written but not built: the pinned .NET SDK 10.0.401 is not installed (BL-1035), so the work was left uncommitted for the shift to stash.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so nothing can be built or tested
