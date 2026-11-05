---
id: BL-425
title: Answer Azure Put Blob and List Blobs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-424]
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

## Acceptance criteria

- [ ] Named tests show:
      - a Put Blob stored and read back;
      - a Put Blob refused when uploads are off;
      - a missing `x-ms-blob-type` is refused as the API says;
      - a bad `Content-MD5` gets `Md5Mismatch`;
      - a List Blobs of five blobs with `maxresults=2` pages through with markers;
      - `delimiter=/` returns `BlobPrefix` elements.
- [ ] Each XML body in the tests is written from the REST API page's example.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
