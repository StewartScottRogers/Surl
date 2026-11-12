---
id: BL-930
title: Forget the folder object on S3 DeleteObject of x/
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-728, BL-1035]
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

- 2026-11-11 (lane 3): Implemented but not built. The code is uncommitted in the lane; the shift stashes it.
  - `ObjectMetadataIndex.ForgetFolderObjectAsync(key, ct)` drops one folder object. An invalid folder-object key throws `ArgumentException`. It rewrites the index file, publishes `StoreChange.Deleted` named `/` plus the key, and returns the write failure or null. A key with no entry does nothing.
  - `S3DeleteObject` is now `AnswerAsync(reply, target, caller, ct)`, with the index and exchange log injected and wired in `S3RequestAnswerer`. For a key ending in `/` it calls `RemoveEmptyDirectory`. Unless that call was refused, it then forgets the `S3IndexKey.OfFolderObject` key (a share has none) and notes an index write failure, as PutObject does.
  - Tests added: four `ForgetFolderObjectAsync_*` in `ObjectMetadataIndexTests`, and four in `S3DeleteObjectTests` (non-empty folder object, index write failure, folder in a writable share, folder in a read-only share). ADR-0108 Amendment 3 point 5 and both libraries' CLAUDE.md are updated.
- Choice: publish `Deleted` for the forgotten folder object, as the index already publishes `MetadataStored`. A stand-in observer then sees `x/` go even when the folder stays. One result: deleting an empty folder publishes two `Deleted` events, the folder's (from `ContentStore`) and the folder object's (`/b/x/`).
- Waits on BL-1035. `dotnet build` cannot run because global.json pins .NET SDK 10.0.401, which is not installed (the machine has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100). On resume: restore the stash, then build, run the fast tests, check coverage and tick the boxes.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: dotnet build cannot find the .NET SDK 10.0.401 that global.json pins; the code is written, but not built or tested
- 2026-11-12: Backlog -> Doing.
