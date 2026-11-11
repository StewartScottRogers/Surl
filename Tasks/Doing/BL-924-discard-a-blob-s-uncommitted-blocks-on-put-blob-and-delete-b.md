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
completed:
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

- [ ] Named tests show Get Block List `uncommitted` empty, and Put Block List of a previously staged block
      `400 InvalidBlockList`, after a Put Blob and after a Delete Blob.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

- Filed by BL-715.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
