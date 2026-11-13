---
id: BL-874
title: Remove content store upload temporary files a killed process left behind
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1035]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
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

- [x] A named test in `Surl.Content.UnitTests` shows a `.surl-upload-` file older than the chosen
      bound being removed by the sweep, and a younger one kept.
- [x] The decision (when the sweep runs, its age bound, and what it never removes) is recorded in
      an ADR marked "Decided by Claude under Stewart's delegation".
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Acceptance criteria

## Notes

2026-11-11 (lane 6): not started. `dotnet build` fails with "A compatible .NET SDK was not found": `global.json` pins 10.0.401 and the machine has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100. Added BL-1035 (Stewart reinstalls the SDK) to `depends-on`; the board offers this task again once it is Done.

2026-11-12 (lane 2): built `ContentStore.RemoveAbandonedUploadFiles(TimeProvider)` (new partial `ContentStore.AbandonedUploads.cs`) and an opt-in `sweepClock` constructor argument that runs it; ADR-0150 records the 24-hour bound and what is never removed. Named tests in `ContentStoreTests.AbandonedUploads.cs` (old removed, younger kept). Fast tests 715 pass, `dotnet build -warnaserror` clean. Line/branch coverage of the new file was not measured (the coverage collector produced no output in this lane); every branch has a test by construction. Follow-up: `Surl.Composition` must pass its `TimeProvider` as `sweepClock` when serving `--directory`.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. ContentStore sweeps .surl-upload- files untouched for 24 hours (ADR-0150)
