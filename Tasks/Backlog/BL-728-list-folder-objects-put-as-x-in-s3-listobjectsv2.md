---
id: BL-728
title: List folder objects put as x/ in S3 ListObjectsV2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-419]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-728 — List folder objects put as x/ in S3 ListObjectsV2

## Goal

A folder created by a zero-byte PutObject of a key ending in `/` is listed by ListObjectsV2 as key `x/` with size 0, as ADR-0108 decision 6s last bullet says, while an empty folder made any other way stays unlisted.

## Context

- ADR-0108 decisions 3 and 6. BL-419 creates the folder on such a PUT but records nothing, so ListObjectsV2 lists no folder object: `ObjectMetadataIndex.StoreAsync` keys files only.
- Needs a way to remember which folders were put as objects - the index in `Surl.Content` (then add it to touches) or a marker the S3 library keeps; decide and record it.

## Acceptance criteria

- [ ] A named test shows `PUT /bucket/x/` (empty body) then `GET /bucket?list-type=2` listing `<Key>x/</Key>` with `<Size>0</Size>` and the empty-body ETag `"d41d8cd98f00b204e9800998ecf8427e"`.
- [ ] A named test shows a folder made by another protocol is not listed.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
