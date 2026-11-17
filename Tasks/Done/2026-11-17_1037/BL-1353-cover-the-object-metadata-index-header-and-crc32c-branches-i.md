---
id: BL-1353
title: Cover the object metadata index header and CRC32C branches in Surl.Content
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1353 — Cover the object metadata index header and CRC32C branches in Surl.Content

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Content.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.95% line, 99.72% branch, 2
  failing members, both in `Foundation/Surl.Content.UnitLibrary/ObjectMetadataIndexFormat.cs`:
  - `ObjectMetadataIndexFormat.HeaderOf(int)` - line 137, 85.71% line, 75% branch; line 142 (`_ => HeaderWithoutContentMd5`,
    the oldest index format) never runs.
  - `IndexReader.ReadCrc32c()` - line 268, 50% branch; line 271 (`presence == 0 ? null : ReadUInt32()`) sees one outcome only.

## Acceptance criteria

- [x] A named `Surl.Content.UnitTests` test reads (or rewrites) an index written in the format before Content-MD5 was
      added, so line 142 runs, and asserts the entries read back.
- [x] Named tests read a version-4 index entry with a CRC32C present and one with it absent, asserting the value and `null`.
- [x] `Measure-CodeQuality.ps1 -Library Surl.Content.UnitLibrary` reports `100 | 100` with 0 failing members, and the
      fast tests are green.

## Notes

- Tests only (test-writer), over the in-memory content file system.
- Line 142 (HeaderOf version 1) was unreachable: VersionOf stopped its loop before asking for version 1. Fixed in ObjectMetadataIndexFormat.VersionOf (loop to version > 0, Require(version > 0)); behaviour unchanged. Added the library to touches; no other Doing task names it.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. Surl.Content.UnitLibrary at 100/100, 0 failing members
