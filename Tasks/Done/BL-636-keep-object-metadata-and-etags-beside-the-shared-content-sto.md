---
id: BL-636
title: Keep object metadata and ETags beside the shared content store for the object-storage servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-622]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-636 — Keep object metadata and ETags beside the shared content store for the object-storage servers

## Goal

`Surl.Content` keeps, for files in the shared content store, the object metadata the object-storage servers need
(content type, user metadata, ETag and whatever else BL-622's ADR lists) in the never-served place the ADR
names, so S3, Azure, Cloud Storage and OCI read the same files HTTP and FTP serve and still answer with their
metadata.

## Context

- Decision: BL-622's ADR, decision 2 (where object metadata and ETags live, its bounds, its byte format, and what
  a file written by FTP - with no metadata - reports when read over S3). Planning assumption: a bounded index
  under `<path>/.surl/objects/`, never served (ADR-0031 decision 5), in memory without `--directory`.
- Library: `Foundation/Surl.Content.UnitLibrary` (`ContentStore`, `IContentFileSystem`, `ContentUploadSession`).
  Metadata follows a file: a rename moves it, a delete or an overwrite by another protocol drops or replaces it as
  the ADR says.
- Persistence follows ADR-0050 decision 7's rules (written through a temporary file and `MoveFileReplacing`, loaded
  once at start, malformed is a load exception carrying the path and reason, a failed write noted not fatal).
- Consumers: BL-419 (S3 PutObject), BL-425 (Azure Put Blob), BL-431 (Cloud Storage writes), BL-437 (OCI pushes).
  This task builds the store side only.

## Acceptance criteria

- [x] Named tests show: metadata stored with an upload is read back; a file with no stored metadata reports the
      ADR's defaults (content type from the media-type table, ETag as the ADR computes it); renaming, deleting and
      overwriting a file through the store update its metadata as the ADR says; the index's bytes are the ADR's
      pinned format; a malformed index raises the load exception with path and reason; the bound returns the ADR's
      typed outcome; `.surl/objects` is never listed or served.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Built `ObjectMetadataIndex` in `Surl.Content` (with `ObjectMetadata`, `ObjectProperties`,
  `ObjectMetadataStoreOutcome`/`Result`, `ObjectMetadataReading`, `ObjectMetadataIndexLoadException`, and the
  internal `ObjectMetadataEntry` and `ObjectMetadataIndexFormat`). Tests: `ObjectMetadataIndexTests` (each
  acceptance clause has a named test: `StoreAsync_ThenReadAsync_ReadsBackTheStoredMetadata`,
  `ReadAsync_FileWithNoStoredMetadata_AnswersTheDefaultsAndCachesTheETag`,
  `ReadAsync_FileOverwrittenThroughTheStore_...`, `ReadAsync_FileRenamedThroughTheStore_...`,
  `ReadAsync_FileDeletedThroughTheStore_...`, `StoreAsync_PersistingIndex_WritesThePinnedBytes`,
  `LoadAsync_Malformed*`, `StoreAsync_MetadataPastTheBound_IsMetadataTooLarge...`,
  `StoreAsync_IndexFullOfCurrentEntries_IsMetadataIndexFull...`, `ContentStore_PersistedIndex_IsNeverListedOrServed`).
  Surl.Content: 488 passed, 1 skipped; 100% line and branch coverage. Solution build clean, fast tests green.
- **The ADR pinned no byte format.** Decided by Claude under Stewart's delegation: header
  `SURL-OBJECT-INDEX-1` LF, 4-byte entry count, entries in strictly ascending ordinal key order: key, length
  (8 signed), last write UTC ticks (8 signed), generation (8 signed), ETag, Content-Type, Content-Encoding,
  Content-Disposition, Cache-Control, Content-Language (empty = absent), 2-byte user-metadata count, name/value
  pairs in strictly ascending ordinal name order; strings are a 2-byte UTF-8 length then bytes; big-endian.
  Ticks rather than seconds so the stamp matches the file system's time exactly. Recorded in
  `ObjectMetadataIndexFormat`'s remarks and the library `CLAUDE.md`; an ADR could not be written here because
  `Documentation/Planning/Decisions` is in BL-641's `touches` (in Doing), so BL-685 carries it into ADR-0100.
- **No media-type table exists** in Surl.Content (HTTP answers everything `application/octet-stream` "until the
  content store knows media types"). The default content type is therefore `application/octet-stream`, which is
  what HTTP names every file today, so the two still agree. BL-686 builds the table for both.
- Other choices (sensible defaults): `MaxMetadataBytes` counts the UTF-8 bytes of the five header fields and the
  user metadata names and values, not the ETag (it caps at 65535 so every string fits its 2-byte length);
  `MaxETagBytes` 1024; a store for a key with no file answers `NoSuchFile`; stale entries are dropped lazily -
  when read, and all at once before the index is called full - rather than stat-ing every entry on every write,
  which with 100000 entries on disk would make each write slow; a computed ETag that finds the index full is
  answered but not cached; metadata at upload with no ETag gets the MD5 computed at store time.
  The index takes `persists: bool` (true with `--directory`) rather than a path, so its location is always
  `<root>/.surl/objects/index`. Wiring it into `Surl.Composition` and calling `LoadAsync` at start is the
  consumers' work (BL-419, BL-425, BL-431, BL-437), outside this task's touches.
- `ContentStore.ServiceStateFolderName` went from private to internal so the index uses the same constant.
- Filed BL-685 (record the format in ADR-0100) and BL-686 (media types).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. ObjectMetadataIndex keeps object metadata and ETags beside the content store, bounded, stamped and persisted under .surl/objects
