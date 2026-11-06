---
id: BL-773
title: Cache each object's CRC-32C in the object metadata index for the gs server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-773 — Cache each object's CRC-32C in the object metadata index for the gs server

## Goal

GET Object and HEAD Object on `gs` take `x-goog-hash`'s CRC-32C and MD5 from the object metadata index instead
of reading the whole file on every request, as ADR-0110 decision 1.1 says ("cached in the index entry beside
the MD5").

## Context

- BL-431 left `Surl.Content` untouched (outside its `touches`), so `GcsObjectPropertiesReader.HashAsync` reads
  every byte of the object for each GET and HEAD to compute both digests. Correct, but a HEAD of a 100 MiB
  object reads 100 MiB.
- `ObjectMetadataIndex` keeps the ETag (the quoted hex MD5) per entry, stamped with the file's length and
  last-write time; `ObjectMetadataIndexFormat` is its on-disk encoding. Add an optional CRC-32C (4 bytes) to the
  entry, its encoding (read old indexes without it), `ObjectMetadata` and `ObjectProperties`.
- PUT Object already computes both digests while it streams the body (`GcsObjectHashes`): store the CRC there.
  A file with no entry computes and caches both on first read, as the ETag is today.

## Acceptance criteria

- [ ] `ObjectMetadataIndex` stores and returns an object's CRC-32C with its stamp; an index file written before
      the change still loads (a named `Surl.Content.UnitTests` test).
- [ ] A named GCS test shows a HEAD of an object PUT over `gs` answers `x-goog-hash` without reading the file
      (for example through a file system fake that counts reads).
- [ ] `dotnet build` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
