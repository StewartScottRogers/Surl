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
completed:
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

- [ ] Named `ContentStoreTests`: a seeded file is read back byte for byte through the read path every file server
      uses; a seed under `.surl` is refused; reset returns to the configured state; snapshot, change, restore gives
      back the same files, folders and object metadata; each publishes its events.
- [ ] A named test with a `UnitTest...` disk-like file system double shows the `--directory` behaviour the ADR
      decides.
- [ ] `dotnet build Foundation/Surl.Content.UnitLibrary -warnaserror` is clean, the whole solution builds, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
