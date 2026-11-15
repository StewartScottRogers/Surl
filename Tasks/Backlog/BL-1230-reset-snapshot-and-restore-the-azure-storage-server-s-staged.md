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
completed:
---
# BL-1230 — Reset, snapshot and restore the Azure Storage server's staged blocks

## Goal

A reset or restore of an instance discards every Azure uncommitted block, staged session and committed block list.

## Context

ADR-0167 decisions 2, 3 and 5. `AzureStagedBlocks` and `AzureStagingArea`; use `DiscardUncommittedAsync` for each, and clear the committed list. Register from Surl.Composition as `StoreKind.Uploads` named `azure`, following `Amqp0ExchangeStoreRegistration`; refuse when the content store is on disk.

## Acceptance criteria

- [ ] After reset, Put Block List naming a block staged earlier answers `InvalidBlockList` and its temporary file is gone.
- [ ] Get Block List after reset answers as for a never-staged blob.
- [ ] Snapshot reports 0 bytes; restore clears as reset does; one `StoreChangedEvent` per operation.
- [ ] A disk-backed content store makes the registration refuse.
- [ ] 100% line and branch coverage for the changed code.

## Notes

## Log

- 2026-11-15: Created.
