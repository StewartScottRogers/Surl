---
id: BL-800
title: Seed, reset, snapshot and restore the content store in Surl.Content
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-789]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-800 — Seed, reset, snapshot and restore the content store in Surl.Content

## Goal

A test can put files (and folders) into the content store before its client connects, return it to its
configured state between tests, and take and restore an in-memory snapshot - files, folders and the object
metadata index - each through `ContentStore`, each published on the event stream.

## Context

- Decision: BL-822's ADR decision 3 (reset target - planning default: configured shares and buckets exist as
  empty folders, plus the files a host supplied at start such as `Surl.Api`'s `Files`; snapshot bound; what each
  does over `DiskContentFileSystem` under `--directory`, where the ADR may refuse reset and restore to protect a
  real directory).
- Where: `Foundation/Surl.Content.UnitLibrary` (`ContentStore.cs`, `InMemoryContentFileSystem.cs`,
  `ObjectMetadataIndex.cs`). Seeding is a write through the store, honouring the `.surl` rule (FR-024) - a seed
  under `.surl` is refused like an upload - but not `--allow-uploads`, which governs peers, not the test.
- Each operation publishes the `StoreChangedEvent`s BL-789 defined (or a summary event, as the ADR says). An
  upload in progress during reset or restore ends as the ADR says.
- Stand-in: store = the content store; controllable. Composition exposes it in BL-805; `Surl.Api` in BL-810.

## Acceptance criteria

- [x] Named `ContentStoreTests`: a seeded file is read back byte for byte through the read path every file server
      uses; a seed under `.surl` is refused; reset returns to the configured state; snapshot, change, restore gives
      back the same files, folders and object metadata; each publishes its events.
- [x] A named test with a `UnitTest...` disk-like file system double shows the `--directory` behaviour the ADR
      decides.
- [x] `dotnet build Foundation/Surl.Content.UnitLibrary -warnaserror` is clean, the whole solution builds, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Delivered as ADR-0124 decision 3 says; no new ADR - the choices below are implementation defaults inside it.
  API on `ContentStore` (new partial `ContentStore.Control.cs`): `SeedFileAsync`, `SeedFolder`, `IsControllable`,
  `ResetAsync(ObjectMetadataIndex?)`, `TakeSnapshotAsync(ObjectMetadataIndex?)`, `RestoreAsync(ContentStoreSnapshot)`;
  new `ContentStoreSnapshot` (public, opaque, `ByteCount`), internal `InMemoryContentFileSystemContents` and
  `ObjectMetadataIndexContents`; `InMemoryContentFileSystem.CaptureContents`/`ReplaceContents` and
  `ObjectMetadataIndex.CaptureEntriesAsync`/`ReplaceEntriesAsync` (internal).
- Choices taken (and why):
  - The index is passed to reset and snapshot, not held by the store: `ContentStore` and `ObjectMetadataIndex` are
    built separately by the composition, and changing the constructor would reach `Surl.Composition` (outside
    `touches`). BL-805 passes `ServiceState`'s index.
  - Configured state = what the in-memory file systems held when the store was constructed (that is where a host's
    `Files` already are), plus each share's folder and each bucket's content path recreated as a folder on reset.
  - "On disk" = any root or share file system that is not `InMemoryContentFileSystem`, or a persisted index. A test
    double that is not the production in-memory type is therefore treated as disk; the named test
    `ResetSnapshotAndRestore_OnADiskLikeFileSystem_AreRefusedAndChangeNothingWhileSeedingIsAllowed` uses
    `UnitTestInMemoryContentFileSystem` as the disk-like double.
  - Seed is the test's write: ignores `--allow-uploads`, `readonly`, share grants and schemes, and the fault plan;
    keeps `.surl`, exposure options (a hidden dot path is `NotPermitted`, as for an upload) and `MaxUploadBytes`.
    It creates missing folders above the file and publishes `Seeded` for each folder created, then the file.
  - Snapshot publishes nothing (it changes nothing; `StoreChange` has no snapshot member); reset/restore publish one
    event named `/`.
  - The `MaxSnapshotBytes` bound across stores is the composer's (BL-805); the store reports `ByteCount`. Snapshots
    share byte arrays with the live store (arrays are replaced, never mutated, on commit), so taking one is cheap.
  - A file with a write stream open (an upload's temporary file) is left out of a capture; replacing contents marks
    every old entry removed, so an in-progress upload fails with `IOException` at its next write or its commit.
- Quality: library 100% line and branch (610 tests in `Surl.Content.UnitTests`). `Measure-CodeQuality.ps1` still lists
  four pre-existing complexity rows in members this task did not change (`ObjectMetadataIndex.StoreAsync`,
  `ContentStore.Map`, `ContentShare.AccessFor`, `ContentStore.ListEntriesAt`); the build's CA1502 gate passes.
## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ContentStore seeds files and folders, resets to its configured state, and takes and restores in-memory snapshots with object metadata, publishing Seeded/Reset/Restored; a store on disk refuses reset, snapshot and restore
