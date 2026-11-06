---
id: BL-930
title: Forget the folder object on S3 DeleteObject of x/
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-728]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-930 — Forget the folder object on S3 DeleteObject of x/

## Goal

A DeleteObject of `x/` removes the folder object `x/` from the object metadata index even when the folder still holds keys, so ListObjectsV2 stops listing `x/` while the keys under it stay listed, as S3 does.

## Context

- ADR-0108 Amendment 3, point 5 (BL-728): today `S3DeleteObject` removes the folder only when empty; for a non-empty folder it answers `204` but the index keeps the folder object, so `x/` is still listed.
- Needs a way to drop one folder object from `ObjectMetadataIndex` (in `Surl.Content`, e.g. a `ForgetFolderObjectAsync`), and `S3DeleteObject` (synchronous today) to call it for a key ending in `/` that has an index key (`S3IndexKey.OfFolderObject`).

## Acceptance criteria

- [ ] A named test in `S3DeleteObjectTests` shows `PUT /bucket/x/` (empty body), a file `x/a.txt`, `DELETE /bucket/x/` answering `204`, then `GET /bucket?list-type=2` listing `x/a.txt` and not `x/`.
- [ ] A named test in `ObjectMetadataIndexTests` shows the folder object dropped and the index file rewritten.
- [ ] ADR-0108 Amendment 3 point 5 states the new behaviour.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
