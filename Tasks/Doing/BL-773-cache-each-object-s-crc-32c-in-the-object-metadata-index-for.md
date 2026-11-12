---
id: BL-773
title: Cache each object's CRC-32C in the object metadata index for the gs server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431, BL-1035]
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

- 2026-11-11 (lane 4): Production code is written but unverified, left uncommitted for the shift
  to stash: `dotnet build` cannot run because global.json pins SDK 10.0.401 and only 10.0.100-rc.1,
  11.0.100-rc.2 and 11.0.100 are installed. The task waits on BL-1035, as BL-771 and BL-760 do.
  Design taken (defaults, recorded here; no new ADR - ADR-0110 decision 1.1 already says the CRC
  is cached beside the MD5):
  - `Surl.Content`: new public `Crc32cChecksum.Append(uint, ReadOnlySpan<byte>)` (0 = no bytes,
    composable), shared by the index and `GcsObjectHashes` so CRC-32C has one implementation.
    `ObjectMetadata.Crc32c` and `ObjectProperties.Crc32c` (`uint?`), `ObjectMetadataEntry` gains
    a positional `uint? Crc32c`. The index computes MD5 and CRC-32C in one read
    (`ComputeDigestsAsync`) on first read and when a store names no ETag; a folder object's CRC is 0;
    a store that names an ETag keeps the `Crc32c` it names (null if none).
  - Format v3: header `SURL-OBJECT-INDEX-3`; each entry ends with one byte 0, or 1 then the 4-byte
    CRC. `-2` (`HeaderWithoutCrc32c`) and `-1` still load with no CRC (`EntryLayout`). A presence
    byte above 1 is malformed.
  - GCS: `GcsObjectDigests.Cached(properties)` builds the digests from the cached CRC and the MD5
    the ETag quotes; null (so the file is hashed) when there is no CRC or the ETag is not a quoted
    32-hex MD5 (multipart `"<hex>-<n>"`). `GcsObjectPropertiesReader.DigestsAsync` replaces the
    `HashAsync` call in `ReadObjectAsync`; a share file's properties carry the CRC from the one hash.
    PUT, resumable and multipart pass the CRC through `StoreMetadataAsync`/`CommitAsync`.
  - Still to do once the SDK is back: update the pinned-bytes test `StoreAsync_PersistingIndex_WritesThePinnedBytes`
    (header `-3`, trailing `01` + CRC-32C of "hello" = `9a71bb4c`), change the "wrong header" malformed
    row from `...2d33` (`-3`) to `...2d34`, add a v2-index load test, a malformed presence-byte test,
    `Crc32cChecksumTests`, `GcsObjectDigests.Cached` tests (no CRC, multipart ETag, uppercase hex),
    and the GCS HEAD-without-read test through a counting `IContentFileSystem` fake; then coverage.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; the code is written and left for the shift to stash
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 4's unverified BL-773 code sits in a shared git stash this run was denied access to (git stash list refused) and a cherry-pick is mid-flight in the lane worktree; SDK 10.0.404 is now installed so the build works - rerun to recover the stash or rewrite
- 2026-11-11: Backlog -> Doing.
