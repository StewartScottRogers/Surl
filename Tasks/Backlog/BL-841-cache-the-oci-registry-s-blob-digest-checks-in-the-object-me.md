---
id: BL-841
title: Cache the OCI registry's blob digest checks in the object metadata index
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-436]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-841 — Cache the OCI registry's blob digest checks in the object metadata index

## Goal

The OCI registry hashes a blob or manifest file once per file stamp (size and last write time) and caches the
result in `ObjectMetadataIndex`, instead of hashing the whole file on every pull.

## Context

- ADR-0115 decision 2.1: "the check runs on the first read and its result is cached in ADR-0100's object
  metadata index against the file's stamp (size and last-write time), so a file replaced by another protocol is
  checked again".
- BL-436 built the check in `OciRepository` (`ReadVerifiedAsync`, `HashMatchesAsync`) without the cache: every
  read hashes the whole file, which is correct but reads a large layer twice before it is served.
- `ObjectMetadataIndex` (`Foundation/Surl.Content.UnitLibrary`) already stamps entries and drops them when the
  file changes. The server needs the index passed to `OciRegistryProtocolServer`, as `S3ProtocolServer` takes it.
  If recording a verified digest needs a new `ObjectMetadata` field, that is a `Surl.Content` change: add it to
  `touches` by the board's rules.

## Acceptance criteria

- [ ] A named test shows a blob pulled twice is hashed once (a counting `UnitTest…ContentFileSystem` fake sees
      one whole-file read before the serve), and a file replaced through the store is hashed again.
- [ ] A tampered file is still `404 BLOB_UNKNOWN` or `MANIFEST_UNKNOWN`, and noted.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-05: Created.
