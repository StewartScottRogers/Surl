---
id: BL-1872
title: Say in the ComposedStores reset, snapshot and restore comments everything the cancellation token cancels
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary/ComposedStores.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1872 — Say in the ComposedStores reset, snapshot and restore comments everything the cancellation token cancels

## Goal

The `cancellationToken` doc comments of the reset and restore methods in `ComposedStores.cs` say every wait the
token cancels.

## Context

Found while running BL-1847. In `Application/Surl.Composition.UnitLibrary/ComposedStores.cs` the whole-instance
reset and restore (and the one-store reset) describe the token as "Cancels the wait for the content store's index
lock." The token also reaches the object servers' upload waits: `S3UploadStoreRegistration`,
`AzureStagedUploadStoreRegistration` and `GcsStagedUploadStoreRegistration` pass it to `AbortOpenUploadsAsync` or
`DiscardStagedUploadsAsync`. Check each of the five store-control methods against what its registrations do with
the token; the two snapshot methods copy no uploads. The matching `SurlServer.cs` comments are already correct.
Comments only.

## Acceptance criteria

- [x] The `cancellationToken` comment of whole-instance reset and whole-snapshot restore names the content store
      wait and the object servers' upload waits.
- [x] The `cancellationToken` comment of the one-store reset names the upload waits as cancellable too.
- [x] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean and the only changed lines in
      `.cs` files are comments.

## Notes

- The comments name S3, Azure Storage and Google Cloud Storage, not "object servers": the fourth uploads entry,
  `OciUploadStoreRegistration`, ignores the token (`AbortOpenUploadsAsync` takes none).
- The two `TakeSnapshotAsync` comments are unchanged and correct: only the content store's snapshot reads the token.
- Three comment edits, made in the session without delegating to `align-and-document`: the change was already
  fully specified here.
- Verified: LF-to-CRLF script rewrote 0 files; `dotnet build -warnaserror` of the solution and of
  `Application/Surl.Composition.UnitTests` clean; `dotnet format --verify-no-changes` exit 0; fast tests 30229
  passed, 3 skipped, 0 failed. Integration tests not run (comments only).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The ComposedStores reset and restore comments now say the token also cancels the wait for the S3, Azure Storage and Google Cloud Storage uploads
