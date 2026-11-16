---
id: BL-1230
title: Reset, snapshot and restore the Azure Storage server's staged blocks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1216, BL-1228]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-15
completed: 2026-11-15
---
# BL-1230 — Reset, snapshot and restore the Azure Storage server's staged blocks

## Goal

A reset or restore of an instance discards every Azure uncommitted block, staged session and committed block list.

## Context

ADR-0167 decisions 2, 3 and 5. `AzureStagedBlocks` and `AzureStagingArea`; use `DiscardUncommittedAsync` for each, and clear the committed list. Register from Surl.Composition as `StoreKind.Uploads` named `azure`, following `Amqp0ExchangeStoreRegistration`; refuse when the content store is on disk.

## Acceptance criteria

- [x] After reset, Put Block List naming a block staged earlier answers `InvalidBlockList` and its temporary file is gone.
- [x] Get Block List after reset answers as for a never-staged blob.
- [x] Snapshot reports 0 bytes; restore clears as reset does; one `StoreChangedEvent` per operation.
- [x] A disk-backed content store makes the registration refuse.
- [x] 100% line and branch coverage for the changed code.

## Notes

- Decision: S3 and Azure both join `StoreKind.Uploads`, but `ComposedStores.ControlOf` finds a control by kind, so `UploadStoreRegistrations.Combine` merges them into one entry (reset and restore run S3 then Azure, one event each). The existing S3 event test now expects the Azure event too, because every extended server is composed whichever listen URLs are given.
- Reset discards every staging (blocks, appends, committed lists) through `AzureStagingArea.DiscardAllInTurnAsync`, which disposes each one, so `DiscardUncommittedAsync` runs for each and the committed list goes with the entry.
- Fast tests: Composition 758 passed, AzureStorage 567 passed plus 3 new; build 0 warnings.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Reset, snapshot and restore of an instance discard the Azure Storage server's staged blocks, appends and committed block lists
