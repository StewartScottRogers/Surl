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
completed:
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

- [ ] `IContentFileSystem.CreateDirectory(string path)` exists with a default body that
      throws `NotSupportedException`, and XML doc comments in the style of the other
      write members.
- [ ] `DiskContentFileSystem.CreateDirectory` exists; an `Integration` test in
      `DiskContentFileSystemTests` creates `a/b` under a temporary directory and finds it.
- [ ] `InMemoryContentFileSystem` (public, sealed) implements every member of
      `IContentFileSystem`, takes a `TimeProvider` and the total-bytes bound ADR-0031
      names (with its named default constant), and starts empty apart from the root
      directory ADR-0031 names.
- [ ] `InMemoryContentFileSystemTests` in `Surl.Content.UnitTests` cover, by name: an
      empty root lists nothing; a written file reads back its bytes, length and last-write
      time; `MoveFileReplacing` replaces an existing file; `DeleteFile` of a missing file
      does nothing; `CreateDirectory` makes missing parents and `GetEntryKind` reports
      them; a write past the bound behaves as ADR-0031 says; two instances share nothing;
      concurrent writes to different paths from parallel tasks all land.
- [ ] A `ContentStoreTests` case composes `ContentStore` over `InMemoryContentFileSystem`
      with `AllowUploads` on, uploads a file and reads it back, and with `AllowUploads`
      off the upload is refused as today.
- [ ] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, the fast tests pass,
      and `Surl.Content.UnitLibrary` keeps 100% line and branch coverage.
- [ ] No test needs `TestCategory=Integration` except the one `DiskContentFileSystem` test.

## Notes

## Log

- 2026-09-29: Created.
