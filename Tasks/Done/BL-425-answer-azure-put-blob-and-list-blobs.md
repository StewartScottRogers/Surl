---
id: BL-425
title: Answer Azure Put Blob and List Blobs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-424, BL-636, BL-656]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-425 — Answer Azure Put Blob and List Blobs

## Goal

The Azure Storage server stores a Put Blob (BlockBlob, only under `--allow-uploads`) and lists a container with
List Blobs, including `prefix`, `delimiter`, `maxresults` and `marker` paging. It also answers Delete Blob if
BL-423's ADR serves it.

## Context

- Decision: BL-423's ADR, decisions 1 and 3.
- Blob REST API: Put Blob (`x-ms-blob-type: BlockBlob` required, `201 Created`, `Content-MD5` returned and checked
  if sent, giving `Md5Mismatch`), and List Blobs (`?restype=container&comp=list`, the `EnumerationResults` XML,
  `BlobPrefix`, `NextMarker`).
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 2): blobs are files in the one shared `ContentStore`.
  Put Blob's content type, `x-ms-meta-*` and ETag go to BL-636's object metadata, not a private store; a file
  written by another protocol is answered with BL-636's defaults. Every store call passes the checked account (or
  anonymous) and scheme to BL-656's share access check; a refusal is answered as BL-423's ADR names it.

## Acceptance criteria

- [x] Named tests show:
      - a Put Blob stored and read back;
      - a Put Blob refused when uploads are off;
      - a missing `x-ms-blob-type` is refused as the API says;
      - a bad `Content-MD5` gets `Md5Mismatch`;
      - a List Blobs of five blobs with `maxresults=2` pages through with markers;
      - `delimiter=/` returns `BlobPrefix` elements.
- [x] Each XML body in the tests is written from the REST API page's example.
- [x] Named tests show a file put into the content store with no metadata (as FTP writes it) read and listed with
      BL-636's defaults, Put Blob metadata read back by Get Blob Properties, and a write refused by share access
      answered with the ADR's error.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Plan: mirror the S3 server's PutObject and ListObjectsV2 (BL-419) for the Blob endpoint, since protocol servers
  may not reference each other: `AzurePutBlob`, `AzureUploadStream`, `AzureListBlobs` with `AzureListRequest`,
  `AzureListPage`, `AzureContainerWalk`, `AzureListMarker`, `AzureNameOrder`; `AzureOperationRecognition` routes
  Get/Put/Delete Blob and List Blobs; `AzureIndexKey` and `AzureBlobPropertiesReader` key and read the index for
  reads and listings alike (a `--bucket` name maps to its content path; a share file answers the defaults).
- Evidence for the criteria (all in `Surl.Extended.Protocol.AzureStorage.UnitTests`):
  `AzurePutBlobTests.ServeAsync_PutBlobThenGetBlob_ReadsTheBlobBack`, `..._PutBlobWithUploadsOff_Is403AuthorizationPermissionMismatch`,
  `..._PutBlobWithoutABlobType_Is400MissingRequiredHeader`, `..._PutBlobWhoseContentMd5IsNotTheBodys_Is400Md5MismatchAndNothingIsStored`,
  `..._PutBlobMetadata_IsReadBackByGetBlobProperties`, `..._PutBlobToAShareThatOnlyGrantsReading_Is403AuthorizationPermissionMismatch`;
  `AzureListBlobsTests.ServeAsync_FiveBlobsTwoAPage_PagesThroughWithMarkers`, `..._DelimiterSlash_ReturnsBlobPrefixElements`,
  `..._ListBlobs_AnswersThePagesEnumerationResults` (the page's example body),
  `..._FilePutByAnotherProtocol_IsListedWithTheIndexDefaultsAndAPutBlobWithItsOwn`; reading an FTP-written file with
  the defaults was already `AzureStorageProtocolServerTests.ServeAsync_GetBlob_ReturnsTheBytesAndHeaders`.
- Coverage: `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.AzureStorage.UnitLibrary` reports 100% line,
  100% branch, 0 failing members, worst CRAP 10.
- Delete Blob is served, as ADR-0109 decisions 1 and 5.6 say: `202`, `404 BlobNotFound`, `403
  AuthorizationPermissionMismatch`.
- Choices where ADR-0109 is silent (recorded in the library's `CLAUDE.md`; ADR wording filed as BL-732, because
  BL-503 holds `Documentation/Planning/Decisions` in Doing):
  - List Blobs needs `--list-directories` (off: `403 AuthorizationPermissionMismatch`): a listing is a directory
    listing in ADR-0006's sense, as ADR-0108 holds for S3's ListObjectsV2; secure by default.
  - A Put Blob body that is unreadable (unknown transfer coding) or cut short is `400 InvalidInput`, the common
    code for a bad request input; a chunked body is served, as decision 4 allows.
  - `OutOfRangeQueryParameterValue` carries `QueryParameterName`, `QueryParameterValue` and `MinimumAllowed`
    (`1`); List Blobs' `InvalidQueryParameterValue` carries a fixed `Reason` per parameter.
  - List Blobs' `Etag` is written without quotes and an absent property as an empty element, as the page's
    example writes them; blobs and `BlobPrefix`es come interleaved in name order; `include=metadata` writes
    `<Metadata />` for a blob with none; the page's other `include` values (and `deletedwithversions`) add nothing.
  - An `x-ms-meta-*` name is checked to be an ASCII C# identifier (letter or `_`, then letters, digits, `_`).
- Not done here, filed: BL-730 (conditional fields on Put Blob and Delete Blob, ADR-0109 decision 1; not in this
  task's criteria) and BL-731 (`x-ms-blob-content-md5`, which needs a Content-MD5 field in `Surl.Content`'s
  object metadata, outside this task's `touches`).
- Two BL-424 tests changed with the behaviour: the 405 rows now use `POST` and `PATCH`, and the "List Blobs, not
  served yet" query row became Get Container Properties (`restype=container`, BL-716).

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-636 and BL-656; uses the shared store's metadata and access check.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Azure server stores Put Blob, answers Delete Blob and pages List Blobs with prefix, delimiter, maxresults and marker on the shared content store
