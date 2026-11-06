---
id: BL-874
title: Remove content store upload temporary files a killed process left behind
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-874 — Remove content store upload temporary files a killed process left behind

## Goal

A `.surl-upload-<guid>` temporary file that `ContentStore` writes for an upload or upload session,
and that a killed process left behind, is removed once a new `ContentStore` serves a `--directory`
folder. So staged OCI blobs, S3, Azure and GCS uploads do not pile up as hidden files.

## Context

- ADR-0130 decision 3: the OCI registry stages upload sessions in `ContentStore` upload sessions,
  whose hidden temporary files sit beside their targets. A clean shutdown or a disposed session
  deletes them, but a killed process does not, and ADR-0115's start-up sweep of `oci-` staging
  folders has nothing to sweep.
- `ContentStore.WriteUploadAsync`, `AppendUploadAsync` and `OpenUploadAsync` name the file
  `.surl-upload-` plus a GUID; `DiskContentFileSystem` is where leftovers outlive the process.
- The sweep must touch no file another live process may be writing. One option: only files older
  than a bound, judged on the injected `TimeProvider`.

## Acceptance criteria

- [ ] A named test in `Surl.Content.UnitTests` shows a `.surl-upload-` file older than the chosen
      bound being removed by the sweep, and a younger one kept.
- [ ] The decision (when the sweep runs, its age bound, and what it never removes) is recorded in
      an ADR marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Acceptance criteria

## Notes

## Log

- 2026-11-05: Created.
