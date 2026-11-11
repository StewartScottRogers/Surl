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
completed: 2026-11-11
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

- [x] A named test shows a blob with only staged blocks listed with `include=uncommittedblobs`, in name order
      among the committed blobs, and not listed without it.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

- Filed by BL-715.
- Plan: `AzureStagingArea.UncommittedBlobs(container, prefix)` picks the `blocks <container>/<prefix>` keys whose
  `AzureStagedBlocks` hold uncommitted blocks, in `AzureNameOrder`; `AzureListBlobs.Merged` merges them into the
  container walk before `AzureListPage` pages and rolls up, so `marker`, `maxresults` and `delimiter` treat them
  as any blob. A name that is both a file and staged is listed once, as the file (its committed blob).
- Decision (sensible default, no curl oracle for an extended server): an uncommitted blob's `Properties` keep the
  committed blob's element order with `Content-Length` 0, `Etag` and the content fields empty elements (there is
  no committed content to describe), `Creation-Time` and `Last-Modified` the last Put Block's time, and the same
  `BlobType`/tier/lease tail; with `include=metadata` it gets `<Metadata />`. The staging area is read inside
  its gate only when `uncommittedblobs` is asked for.
- Tests: `AzureListBlobsTests.ServeAsync_IncludeUncommittedBlobs_ListsABlobWithOnlyStagedBlocksInNameOrder` and
  `ServeAsync_IncludeUncommittedBlobsAndMetadataWithAPrefix_ListsTheStagedBlobUnderThePrefixWithEmptyMetadata`;
  the library is at 100% line and branch coverage (cobertura), 515 tests green.
- Environment: the machine has lost .NET SDK 10.0.4xx (only 10.0.100-rc.1 and 11.0.100 are installed, 11.0 dated
  2026-11-10), so `global.json` (10.0.401, latestFeature) resolves no SDK and `dotnet build` fails before compiling.
  Verified by rolling `global.json` forward to `latestMajor` locally (SDK 11.0.100, net10.0 target, runtime
  10.0.15): solution build with `-warnaserror` clean, every fast test green; `global.json` restored, not committed.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. List Blobs with include=uncommittedblobs lists blobs that have only staged blocks, in name order, Content-Length 0
