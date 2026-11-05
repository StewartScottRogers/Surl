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
completed:
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

- [ ] Named tests show:
      - a Put Blob stored and read back;
      - a Put Blob refused when uploads are off;
      - a missing `x-ms-blob-type` is refused as the API says;
      - a bad `Content-MD5` gets `Md5Mismatch`;
      - a List Blobs of five blobs with `maxresults=2` pages through with markers;
      - `delimiter=/` returns `BlobPrefix` elements.
- [ ] Each XML body in the tests is written from the REST API page's example.
- [ ] Named tests show a file put into the content store with no metadata (as FTP writes it) read and listed with
      BL-636's defaults, Put Blob metadata read back by Get Blob Properties, and a write refused by share access
      answered with the ADR's error.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-636 and BL-656; uses the shared store's metadata and access check.
