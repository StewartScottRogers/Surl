---
id: BL-086
title: Write uploads to a temporary file and rename it into place in Surl.Content
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests, Surl.Protocol.Tftp.UnitTests, Documentation/Planning/Decisions/ADR-0013-how-the-tftp-server-answers.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
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

- [x] A `Surl.Content.UnitTests` test proves that an upload which throws partway leaves an
      existing file's bytes unchanged and leaves no temporary file behind.
- [x] A test proves the same for a `TooLarge` upload.
- [x] A test proves that a `Written` upload replaces the file's bytes and leaves no
      temporary file.
- [x] The `WriteUploadAsync` XML remarks and ADR-0013 amendment 1 say that a replaced
      file is kept until the upload is written.
- [x] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports no failing member in `Surl.Content.UnitLibrary`.

## Notes

2026-09-29, dark factory lane 2: acceptance criterion 4 amends ADR-0013 (amendment 1), so
`Documentation/Planning/Decisions/ADR-0013-how-the-tftp-server-answers.md` is added to
`touches`. BL-061, in Doing, touches `Documentation/Planning/Decisions`, which contains it,
so this task returns to Backlog until BL-061 leaves Doing. No code was started.

2026-09-29, dark factory lane 3:
- Added `Surl.Protocol.Tftp.UnitTests` to `touches`: its `InMemoryContentFileSystem`
  implements the seam's write members, so without `MoveFileReplacing` every accepted TFTP
  upload in its tests would hit the default `NotSupportedException`. No task in Doing
  names it.
- Seam: `IContentFileSystem.MoveFileReplacing(source, destination)`, default throws
  `NotSupportedException`; `DiskContentFileSystem` calls
  `File.Move(source, destination, overwrite: true)`.
- Choice: the temporary name is `.surl-upload-<GUID, 32 hex digits>` in the target's
  directory. It leaves the target's own name out so it never exceeds a file-name length
  limit, and the fresh GUID keeps two concurrent uploads to one file from sharing a
  temporary file. A dot-file is hidden and unlisted unless
  `ContentExposureOptions.ServeDotFiles` is on; with it on, a half-written temporary file can be read, which is
  what that option asks for.
- A rename that throws deletes the temporary file too and the target is kept (tested with
  a failing fake rename).
- Tests: 5 new fast tests and 2 new Integration tests on disk; Surl.Content 100% line and
  branch, 0 failing members, worst CRAP 8.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Documentation/Planning/Decisions/ADR-0013 (amendment 1), inside Documentation/Planning/Decisions, which BL-061 in Doing touches; resume once BL-061 leaves Doing.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Content writes each upload to a temporary dot-file and renames it over the target only when Written, so a failed, cut-off or too-large upload keeps the old file
