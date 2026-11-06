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
completed: 2026-11-06
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

- [x] Named tests show three blocks staged, committed in order with Put Block List, and read back with Get Blob.
- [x] Named tests show a block ID that is not Base64 or of another length than the blob's other blocks `400
      InvalidBlockId`, a list naming an unstaged block `400 InvalidBlockList`, and a staged blob invisible to the
      content store until committed.
- [x] Named tests show Get Block List's `BlockList` XML written from the page's example, with
      `blocklisttype=committed|uncommitted|all`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-423 (ADR-0109): BL-425 serves Put Blob alone; the port's client needs blocks from 8 MiB.
- Delivered (2026-11-06): `AzurePutBlock`, `AzurePutBlockList`, `AzureGetBlockList`, `AzureStagedBlocks`,
  `AzureBlock`, `AzureBlockId`, `AzureBlockListDocument` (+ `AzureBlockListKind`, `AzureBlockListEntry`),
  `AzureBoundedBodyStream`; five new `AzureError` codes; three new `AzureOperation`s recognised by method and
  `comp`. `AzurePathStagingArea` became `AzureStagingArea` over `IAzureStaging`, holding Data Lake appends and
  blobs' blocks together. Tests: `AzureBlockUploadTests` (the acceptance tests, e.g.
  `ServeAsync_ThreeBlocksCommittedInOrder_AreReadBackWithGetBlob`,
  `ServeAsync_PutBlockWithAMalformedId_Is400InvalidBlockId`,
  `ServeAsync_PutBlockWithAnIdOfAnotherLength_Is400InvalidBlockId`,
  `ServeAsync_PutBlockListNamingAnUnstagedBlock_Is400InvalidBlockListAndWritesNothing`,
  `ServeAsync_StagedBlob_IsInvisibleUntilCommitted`, `ServeAsync_GetBlockList_WritesThePagesBlockListForEachType`),
  `AzureBlockListDocumentTests`, `AzureBoundedBodyStreamTests`, `AzureStagingAreaTests`. Azure tests 368, all green;
  every member written here at 100% line and branch.
- Choices (Decided by Claude under Stewart's delegation; recorded in ADR-0109 by BL-922):
  - Where blocks are staged: ADR-0109 says `.surl/objects/uploads/`, but `ContentStore` refuses every write
    under `/.surl` and BL-715 touches only this library. So each blob's blocks sit, one after another, in a
    `ContentUploadSession` opened at the blob and never committed - a hidden dot-file beside it, as Data Lake
    appends already are. The blob stays invisible; Put Block List copies the blocks into a fresh session and
    commits that.
  - The blob's folders are created at its first Put Block, because the staging file must sit in them.
  - A blob's staged bytes together are held to `--max-filesize` (the session's own limit): past it, all its
    uncommitted blocks are discarded and the answer is `413`. A block put again under the same ID replaces
    the old one; its old bytes stay in the staging file until commit.
  - The committed block list is kept in memory against the file's `ContentFileStatus` (length and last-write
    time). A blob written another way has no committed blocks; a `Committed` entry for it is `InvalidBlockList`.
  - The 1000-staging cap ("1000 blobs with staged blocks or appends per server") is shared. At the cap, the
    least recently touched staging holding only a committed list is forgotten, so remembered lists never
    cause `503`.
  - `blocklisttype` is matched case-insensitively and echoed as sent when refused. Get Block List sends
    `Last-Modified` and `ETag` only when the blob exists. Put Block List answers `ETag` (the blob's MD5, as
    Put Blob's), `Last-Modified` and `x-ms-request-server-encrypted: false`.
  - A Put Block List body is read with `XmlReader` with DTDs prohibited and no resolver; an unknown child
    element, text in the root or an element inside an entry is `InvalidXmlDocument`.
- `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.AzureStorage.UnitLibrary` still lists three
  `AzureLoginJudge` methods over Cobertura complexity 10. They were already there, in a file this task did not
  change, and CA1502 passes them. Filed as BL-925.
- Filed: BL-922 (ADR-0109 amendment), BL-923 (`include=uncommittedblobs`), BL-924 (Put Blob and Delete Blob
  discard uncommitted blocks), BL-925 (AzureLoginJudge complexity).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. The Azure server stages blocks with Put Block, commits them with Put Block List and lists them with Get Block List
