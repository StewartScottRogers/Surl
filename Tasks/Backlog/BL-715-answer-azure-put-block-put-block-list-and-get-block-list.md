---
id: BL-715
title: Answer Azure Put Block, Put Block List and Get Block List
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-425]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-715 — Answer Azure Put Block, Put Block List and Get Block List

## Goal

The Azure Storage server stages blocks with Put Block (`?comp=block&blockid=`), commits them with Put Block List
(`?comp=blocklist`, `Latest`, `Committed` and `Uncommitted` entries), and lists them with Get Block List, so a
client that uploads a large file in blocks completes the upload.

## Context

- Decision: ADR-0109 decisions 1, 3 and 7 (blocks staged under ADR-0100's `.surl/objects/uploads/`, invisible
  until committed; 100000 uncommitted and 50000 committed blocks; an uncommitted block list dropped after 7 days,
  as the Put Block page says).
- Specifications: "Put Block", "Put Block List", "Get Block List", "Blob service error codes" (`InvalidBlockId`,
  `InvalidBlockList`, `BlockCountExceedsLimit`, `BlockListTooLong`).
- The Curl port's `az` client uploads from 8 MiB in equal-length Base64 blocks then a block list (its matrix rows
  `PutBlock-1` to `PutBlockList-2`); that is what to accept, not a verdict.

## Acceptance criteria

- [ ] Named tests show three blocks staged, committed in order with Put Block List, and read back with Get Blob.
- [ ] Named tests show a block ID that is not Base64 or of another length than the blob's other blocks `400
      InvalidBlockId`, a list naming an unstaged block `400 InvalidBlockList`, and a staged blob invisible to the
      content store until committed.
- [ ] Named tests show Get Block List's `BlockList` XML written from the page's example, with
      `blocklisttype=committed|uncommitted|all`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-423 (ADR-0109): BL-425 serves Put Blob alone; the port's client needs blocks from 8 MiB.

## Log

- 2026-11-05: Created.
