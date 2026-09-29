---
id: BL-091
title: Add a production InMemoryContentFileSystem and a CreateDirectory member to the content file-system seam
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-090]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: FR-022
created: 2026-09-29
completed: 2026-09-29
---
# BL-091 — Add a production InMemoryContentFileSystem and a CreateDirectory member to the content file-system seam

## Goal

`Surl.Content.UnitLibrary` ships `InMemoryContentFileSystem`, a production
`IContentFileSystem` that holds files and directories in memory, and the seam gains
`CreateDirectory`, so surl can serve without touching the disk and services can create
their `.surl/<service>` folders through the seam.

## Context

FR-022 and FR-023; ADR-0031 (BL-090) decides the in-memory served root path, the bound on
total bytes and what an upload past it gets, and the last-write time reported.

- `Surl.Content.UnitLibrary/IContentFileSystem.cs`: the seam. Its write members
  (`CreateFileForAsyncWrite`, `DeleteFile`, `MoveFileReplacing`) have default bodies that
  throw `NotSupportedException`; add `void CreateDirectory(string path)` the same way
  (creates the directory and any missing parents; does nothing when it exists).
- `Surl.Content.UnitLibrary/DiskContentFileSystem.cs`: implement `CreateDirectory` as a
  thin `Directory.CreateDirectory` call, excluded from coverage with a justifying comment
  like its other members, and proved by an `Integration` test in `DiskContentFileSystemTests`.
- `Surl.Content.UnitTests/UnitTestInMemoryContentFileSystem.cs` is the tests' own fake and
  a useful model, but it records calls and has test-only switches; the production class is
  new, in `Surl.Content.UnitLibrary/InMemoryContentFileSystem.cs`, safe for concurrent use
  (every protocol server and connection shares one store), with no test-only members.
- The store writes an upload to a temporary dot-file, then `MoveFileReplacing`s it into
  place (BL-086), so the in-memory class must support that sequence, and a read stream
  opened before a replace keeps reading the old bytes.
- No symbolic links exist in memory: `ResolveFinalPath` returns the normalised path.
- Paths are the platform's full paths, as `Path.Join` builds them from the served root;
  compare them ordinally.
- `TimeProvider` is injected for last-write times (root `CLAUDE.md`).

## Acceptance criteria

- [x] `IContentFileSystem.CreateDirectory(string path)` exists with a default body that
      throws `NotSupportedException`, and XML doc comments in the style of the other
      write members.
- [x] `DiskContentFileSystem.CreateDirectory` exists; an `Integration` test in
      `DiskContentFileSystemTests` creates `a/b` under a temporary directory and finds it.
- [x] `InMemoryContentFileSystem` (public, sealed) implements every member of
      `IContentFileSystem`, takes a `TimeProvider` and the total-bytes bound ADR-0031
      names (with its named default constant), and starts empty apart from the root
      directory ADR-0031 names.
- [x] `InMemoryContentFileSystemTests` in `Surl.Content.UnitTests` cover, by name: an
      empty root lists nothing; a written file reads back its bytes, length and last-write
      time; `MoveFileReplacing` replaces an existing file; `DeleteFile` of a missing file
      does nothing; `CreateDirectory` makes missing parents and `GetEntryKind` reports
      them; a write past the bound behaves as ADR-0031 says; two instances share nothing;
      concurrent writes to different paths from parallel tasks all land.
- [x] A `ContentStoreTests` case composes `ContentStore` over `InMemoryContentFileSystem`
      with `AllowUploads` on, uploads a file and reads it back, and with `AllowUploads`
      off the upload is refused as today.
- [x] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, the fast tests pass,
      and `Surl.Content.UnitLibrary` keeps 100% line and branch coverage.
- [x] No test needs `TestCategory=Integration` except the one `DiskContentFileSystem` test.

## Notes

- Delivered in-session rather than through the full `/feature` agent chain: one library and
  its tests, the design fixed by ADR-0031 decision 4, and no wire bytes involved, so no
  conformance stage applies.
- `RootPath` is `public static readonly`, not `const`, because it depends on the platform;
  it comes from the internal `RootPathFor(bool isWindows)` so both answers are tested on any
  platform and branch coverage stays 100%.
- Paths are normalised with `Path.GetFullPath` and `Path.TrimEndingDirectorySeparator`, then
  compared ordinally; `ResolveFinalPath` is that normalisation.
- Failures mirror the disk's exception types: a missing file `FileNotFoundException`, a
  missing directory `DirectoryNotFoundException`, a directory where a file is wanted
  `UnauthorizedAccessException`, a file in the way of `CreateDirectory` `IOException`.
  `DeleteFile` of a directory does nothing (nothing is a file there).
- A file's bytes appear when its write stream is disposed; before that it exists, empty.
  Every accepted write is charged against `MaxTotalBytes` at once, so files still being
  written count (ADR-0031: temporary upload files included). A write after the file was
  deleted or replaced throws `IOException` rather than growing a detached buffer the bound
  no longer counts.
- `MoveFileReplacing` onto itself is a no-op, as `File.Move` with `overwrite` is.
- Added `TotalBytes` (public) so callers and tests can see what the bound is measured
  against, and `SettableTimeProvider` in the tests (no package; the SDK has no fake clock).
- The existing Integration-only `DiskContentFileSystemTests` gained
  `CreateDirectory_MissingParent_CreatesBoth`; the ContentStore cases are
  `WriteUploadAsync_InMemoryFileSystemAllowingUploads_IsWrittenAndReadsBack` and
  `WriteUploadAsync_InMemoryFileSystemWithoutAllowUploads_IsNotPermitted`.
- Verified: `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean for
  both projects, fast tests green solution-wide (Surl.Content.UnitTests 241 passed, 1
  skipped as before), `Surl.Content.UnitLibrary` 100% line and 100% branch coverage.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. InMemoryContentFileSystem serves and takes uploads without a disk, and the seam has CreateDirectory
