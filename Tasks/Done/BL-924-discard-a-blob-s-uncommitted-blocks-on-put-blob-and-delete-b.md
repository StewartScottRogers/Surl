---
id: BL-924
title: Discard a blob's uncommitted blocks on Put Blob and Delete Blob
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-715]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-924 — Discard a blob's uncommitted blocks on Put Blob and Delete Blob

## Goal

Put Blob and Delete Blob discard the blob's uncommitted blocks, as the "Put Blob" and "Delete Blob" pages
say the service does, so a later Put Block List cannot commit blocks staged before them.

## Context

- BL-715 stages blocks in `AzureStagedBlocks` (server-wide `AzureStagingArea`, key
  `AzureStagedBlocks.KeyOf`). `AzurePutBlob` and `AzureBlobRequestAnswerer.DeleteBlob` do not touch it, so
  uncommitted blocks outlive a Put Blob or Delete Blob until the 7-day expiry. The committed list is already
  forgotten, because it is held against the file's length and last-write time.
- Call `AzureStagingArea.DiscardInTurnAsync(AzureStagedBlocks.KeyOf(target), …)` after the write or delete
  succeeds, as Path - Create and Path - Delete do for Data Lake appends.

## Acceptance criteria

- [x] Named tests show Get Block List `uncommitted` empty, and Put Block List of a previously staged block
      `400 InvalidBlockList`, after a Put Blob and after a Delete Blob.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

- Filed by BL-715.
- `AzureBlobRequestAnswerer.DiscardingStagedBlocksAsync` wraps Put Blob and Delete Blob and calls
  `AzureStagingArea.DiscardInTurnAsync(AzureStagedBlocks.KeyOf(target))` when the answer's status is below
  300 (201 or 202). `HttpAnswer.IsRefusal` could not be the test: `AzureReply.Error` builds its answers
  through `Answer`, so they carry `IsRefusal: false`.
- Tests (`AzureBlockUploadTests`): `ServeAsync_PutBlobAfterStagedBlocks_DiscardsTheUncommittedBlocks`,
  `ServeAsync_DeleteBlobAfterStagedBlocks_DiscardsTheUncommittedBlocks` (after a Delete Blob nothing is
  left, so Get Block List `uncommitted` is `404 BlobNotFound`, the "no blob and no uncommitted block"
  answer - the empty list the criterion means), and `ServeAsync_RefusedDeleteBlob_KeepsTheUncommittedBlocks`.
- Verified with SDK 11.0.100 from a folder outside the repository, because the pinned SDK 10.0.401 is not
  installed on this machine (BL-1035): solution build `-warnaserror` clean, every fast test green,
  library 100% line and branch coverage.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Put Blob and Delete Blob discard the blob's uncommitted blocks, so a later Put Block List cannot commit them
