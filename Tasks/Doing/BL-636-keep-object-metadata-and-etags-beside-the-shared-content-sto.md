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
completed:
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

- [ ] Named tests show: metadata stored with an upload is read back; a file with no stored metadata reports the
      ADR's defaults (content type from the media-type table, ETag as the ADR computes it); renaming, deleting and
      overwriting a file through the store update its metadata as the ADR says; the index's bytes are the ADR's
      pinned format; a malformed index raises the load exception with path and reason; the bound returns the ADR's
      typed outcome; `.surl/objects` is never listed or served.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
