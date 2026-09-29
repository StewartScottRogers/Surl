---
id: BL-047
title: Apply the default exposure rules and the upload limit in Surl.Content
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-010, BL-044]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-047 — Apply the default exposure rules and the upload limit in Surl.Content

## Goal

`ContentStore` takes ADR-0006's exposure settings and upload limit as constructor
options and applies them for every protocol server: uploads refused, directory listings
refused, symbolic links not followed, dot-files hidden - each by default - and an
accepted upload that grows past `MaxUploadBytes` stopped and its partial file deleted.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`,
  section 2 ("What a server exposes by default") and the "Maximum upload" row of
  section 1, with section 6's rule that `Surl.Content` takes these as constructor
  options "so every protocol server gets the same answers".
- Defaults, and the option each one mirrors (parsed later by `Surl.Cli`, BL-003/BL-014):
  | Setting | Default | Option |
  | --- | --- | --- |
  | Uploads | off: refused as "not permitted" | `--allow-uploads` |
  | Directory listings | off: answered as absent | `--list-directories` |
  | Symbolic links, junctions, reparse points | not followed: answered as absent | `--follow-symlinks` (only when the final target resolves inside the root) |
  | Dot-files (any path segment starting with `.`) | hidden: answered as absent and left out of every listing | `--serve-dot-files` |
  | `MaxUploadBytes` | 104857600; 0 is no limit | `--max-filesize` |
- "Answered as absent" means the store's result for a hidden dot-file, an unfollowed
  link and a refused listing is the same value it returns for a path that does not
  exist, so no protocol server can answer them differently (ADR-0006 section 2). A
  refused upload is a distinct "not permitted" result, because each protocol answers it
  with its own code (HTTP 405 with `Allow`, FTP 550, TFTP error 2). A link whose target
  resolves outside the root stays refused whatever the options
  (`Surl.Content.UnitLibrary/CLAUDE.md`).
- Where to start: `ContentStore.cs` (constructor `ContentStore(string servedRoot, IContentFileSystem fileSystem)`),
  `ContentPathMapping.cs`, `ContentPathRefusal.cs`, `IContentFileSystem.cs`, BL-044's
  listing member, and BL-010's `System.IO` implementation. Name the options type for what
  it holds (for example `ContentExposureOptions`), with defaults equal to the table.
- Uploads: the store has no write path yet. Add one to the seam and the store: write an
  upload from a stream through the seam, counting bytes, and once the count would pass
  `MaxUploadBytes` stop reading (never read more than the limit plus the one read that
  crossed it), delete the partial file through the seam, and return an
  "upload too large" result. The `System.IO` implementation's new members get
  `[ExcludeFromCodeCoverage(Justification = "…")]` and an `Integration` test in a
  temporary directory, as BL-010 did.
- This library sends no bytes to a peer. Each protocol server's answer for these results
  is measured against pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1`
  (ADR-0003) by that server's task before it is pinned.

## Acceptance criteria

- [x] `ContentStore` takes the options type, and
      `ContentExposureOptionsTests.Default_RefusesUploadsAndListingsHidesDotFilesAndLinks`
      pins the defaults in the table above.
- [x] Fast tests with the in-memory fake prove, with default options, that
      `/.git/config`, `/.hidden` and `/dir/.env` map to the same result as a missing
      path; that a listing of a directory with dot-files omits them; that a symbolic link
      whose target is inside the root maps to the missing-path result; and that a
      listing request maps to the missing-path result.
- [x] Fast tests prove that with the dot-file, listing and link options on, those same
      paths are served and listed, and that a link whose target is outside the root is
      refused with every option on.
- [x] Fast tests prove an upload with uploads off returns the "not permitted" result
      without creating a file; an upload of exactly `MaxUploadBytes` succeeds; one byte
      more returns "upload too large" and leaves no file behind in the fake; and
      `MaxUploadBytes = 0` accepts any size.
- [x] A `[TestCategory("Integration")]` test writes an over-limit upload to a temporary
      directory through the `System.IO` implementation and finds no file afterwards.
- [x] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Content.UnitLibrary`.

## Notes

- Decisions recorded in ADR-0015 (decided by Claude under Stewart's delegation). In short:
  `ContentExposureOptions` record with ADR-0006's defaults; hidden paths map with
  `EntryKind` `None` and an internal flag so every later look through the store also says
  nothing is there; a link inside the root is detected by comparing the resolved path with
  the path joined to the resolved root; `WriteUploadAsync` returns `ContentUploadResult`
  (`Written`, `NotPermitted`, `TooLarge`), reads at most one byte past the limit, and
  deletes the partial file when too large or when the copy throws.
- `touches` widened to `Documentation/Planning/Decisions` for ADR-0015 and its index row;
  no task in `Doing` names it (BL-036 MQTT, BL-037 TFTP).
- Kept `ContentStore(string, IContentFileSystem)`, serving with
  `ContentExposureOptions.ServeEverythingInsideTheRoot` (the store's old behaviour, uploads
  off), because `Surl.Console` and the HTTP, Gopher, DICT and TFTP tests build stores with
  it and are outside this task's `touches`. For the same reason the seam's new members
  (`CreateFileForAsyncWrite`, `DeleteFile`) are default interface members that throw
  `NotSupportedException`, so those projects' read-only fakes compile unchanged.
- `Path.GetDirectoryName(string)` rewrites `/` to `\` on Windows, which broke the
  ordinal seam lookup of an upload's parent directory; the span overload keeps the
  spelling.
- Follow-ups filed: BL-070 (pass the options `Surl.Cli` already parses from
  `Surl.Console`), BL-069 (move every other caller to the options constructor, remove the
  two-argument one, and decide how DICT reads its database under default options).
- Verified: `dotnet build` clean; fast tests green solution-wide; `Surl.Content.UnitTests`
  188 passed with Integration included (8 skipped are non-Windows link tests);
  `Measure-CodeQuality.ps1 -Library Surl.Content.UnitLibrary` reports 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ContentStore applies ADR-0006's exposure defaults and the upload limit through ContentExposureOptions
