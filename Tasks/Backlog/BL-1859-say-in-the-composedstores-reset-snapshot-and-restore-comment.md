---
id: BL-1859
title: Say in the ComposedStores reset, snapshot and restore comments everything the cancellation token cancels
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary/ComposedStores.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1859 — Say in the ComposedStores reset, snapshot and restore comments everything the cancellation token cancels

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

- [ ] The `cancellationToken` comment of whole-instance reset and whole-snapshot restore names the content store
      wait and the object servers' upload waits.
- [ ] The `cancellationToken` comment of the one-store reset names the upload waits as cancellable too.
- [ ] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean and the only changed lines in
      `.cs` files are comments.

## Notes

## Log

- 2026-11-27: Created.
