---
id: BL-1526
title: State in Surl.Api that Files.DeleteAsync leaves the folders on the path
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Surl.Api]
requirement: none
created: 2026-11-20
completed: 2026-11-21
---
# BL-1526 — State in Surl.Api that Files.DeleteAsync leaves the folders on the path

## Goal

`SurlServerFiles.DeleteAsync`'s XML comment and the Surl.Api README say that deleting a folder's only file leaves the folder (empty), as a client's delete does, while `PutAsync` makes any missing folder - so a test is not surprised by an empty folder in `Files.ListAsync`.

## Context

Surly cookbook bug report 2026-11-20, item 7, first half (Surl `9ad60d80c`; unchanged at `299737d24`): `Files.DeleteAsync` on the only file in a folder leaves the empty folder. That is the intended behaviour, not a defect to change: ADR-0161 decision 1 makes `delete_file` / `Files.DeleteAsync` "the test's own delete", applying "as a client's delete would", and a client's FTP `DELE` or HTTP `DELETE` of a file removes no folder. What is missing is the statement, beside `PutAsync`'s "making any folder on the path" (`Application/Surl.Api/SurlServerFiles.cs:24`), which makes the asymmetry look like an oversight.

Where to write it:
- `Application/Surl.Api/SurlServerFiles.cs:47-56`, the `<summary>` of `DeleteAsync`: add that folders on the path stay, empty or not, and that `SurlServer.ResetAsync` (or a snapshot restore) is how a test returns the content store to its configured state.
- `Application/Surl.Api/README.md`, "Seed, reset, snapshot" (around line 254, the `Files.DeleteAsync` line of the example): the same, in one comment or sentence.

No behaviour changes; this is a docs task, so no test is added. Stand-in: tests that list folders after a delete read the empty folder; a test that needs it gone resets the store.

## Acceptance criteria

- [x] `SurlServerFiles.DeleteAsync`'s XML `<summary>` states that the folders on the path are left in place, as a client's delete leaves them (ADR-0161 decision 1), and names `SurlServer.ResetAsync` as the way back to the configured state.
- [x] `Application/Surl.Api/README.md`'s "Seed, reset, snapshot" section states the same.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. Stated in DeleteAsync summary and README that folders stay; build clean
