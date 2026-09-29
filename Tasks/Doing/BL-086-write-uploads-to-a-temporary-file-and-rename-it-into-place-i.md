---
id: BL-086
title: Write uploads to a temporary file and rename it into place in Surl.Content
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-086 — Write uploads to a temporary file and rename it into place in Surl.Content

## Goal

`ContentStore.WriteUploadAsync` leaves a file it would replace untouched until the whole
upload is written. It writes to a temporary file beside the target and renames it over the
target only when the result is `Written`. So an upload that ends early, is too large, or
fails leaves the old file exactly as it was.

## Context

- Found by the BL-054 review: the store creates the target, which empties an existing file,
  before its first read of the upload. For TFTP that first read is what sends the OACK or
  ACK 0. A WRQ whose client then sends an ERROR or goes silent therefore deletes the old
  file and writes nothing in its place. TFTP has no authentication, so with
  `--allow-uploads` one spoofed WRQ can remove a file. HTTP `PUT`, FTP `STOR` and SFTP
  writes lose a file the same way when the upload is cut off.
- `Surl.Content.UnitLibrary/ContentStore.cs` (`WriteUploadAsync`, `CopyWithinUploadLimitAsync`)
  and `IContentFileSystem` (`CreateFileForAsyncWrite`, `DeleteFile`). The rename needs a new
  seam member, e.g. `MoveFileReplacing(string source, string destination)` over
  `File.Move(source, destination, overwrite: true)`, with a default that throws
  `NotSupportedException` like the other write members. The temporary name must be a
  dot-file in the same directory, so the store never serves or lists it.
- The rule "a file the upload replaced is gone", in the `WriteUploadAsync` remarks and in
  ADR-0013 amendment 1, changes to "a file the upload would replace is kept".

## Acceptance criteria

- [ ] A `Surl.Content.UnitTests` test proves that an upload which throws partway leaves an
      existing file's bytes unchanged and leaves no temporary file behind.
- [ ] A test proves the same for a `TooLarge` upload.
- [ ] A test proves that a `Written` upload replaces the file's bytes and leaves no
      temporary file.
- [ ] The `WriteUploadAsync` XML remarks and ADR-0013 amendment 1 say that a replaced
      file is kept until the upload is written.
- [ ] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports no failing member in `Surl.Content.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
