---
id: BL-923
title: List uncommitted blobs with List Blobs include=uncommittedblobs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-715]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-923 — List uncommitted blobs with List Blobs include=uncommittedblobs

## Goal

List Blobs with `include=uncommittedblobs` also lists each blob that has blocks staged by Put Block and no
committed file, as ADR-0109 decision 5.3 says.

## Context

- ADR-0109 decision 5.3: "`uncommittedblobs` lists blobs staged by Put Block and not committed (BL-715)".
  BL-715 served the blocks but left the listing: today `include=uncommittedblobs` is accepted and adds nothing.
- The staged blocks are `AzureStagedBlocks` in the server's `AzureStagingArea`, keyed by
  `AzureStagedBlocks.KeyOf`; `AzureListBlobs` and `AzureContainerWalk` build the listing.
- Specification: "List Blobs", the `include` parameter and the `Properties` of an uncommitted blob
  (`Content-Length` 0).

## Acceptance criteria

- [ ] A named test shows a blob with only staged blocks listed with `include=uncommittedblobs`, in name order
      among the committed blobs, and not listed without it.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

- Filed by BL-715.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-923-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-923-lane-6-20261111-060243 and fix it.
