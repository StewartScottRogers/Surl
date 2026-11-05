---
id: BL-419
title: Answer S3 PutObject, ListObjectsV2 and virtual-hosted requests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-418, BL-636, BL-656]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-419 — Answer S3 PutObject, ListObjectsV2 and virtual-hosted requests

## Goal

The S3 server stores a `PutObject` body (only when uploads are allowed), lists a bucket with ListObjectsV2
including paging, prefixes and delimiters, and answers virtual-hosted-style requests as it answers path-style ones.

## Context

- Decision: BL-417's ADR, decisions 1, 2 and 6. Uploads need `--allow-uploads` (ADR-0006 section 2), passed
  to the server's constructor and not read from a static.
- S3 API Reference: PutObject (`ETag` is the MD5 of the body for a single-part upload; `Content-MD5` is checked
  if sent, giving `BadDigest`), ListObjectsV2 (the `ListBucketResult` XML, `KeyCount`, `IsTruncated`,
  `NextContinuationToken`, `CommonPrefixes`), DeleteObject if the ADR serves it, and "Virtual hosting of buckets".
- Body reading comes from BL-407's library (`Content-Length`, chunked, `aws-chunked` if the ADR
  serves it).
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 2): objects are files in the one shared
  `ContentStore` that HTTP, FTP, SFTP and SMB serve. PutObject's content type, `x-amz-meta-*` and ETag are kept in
  BL-636's object metadata, not in a private store, and an object written by another protocol (no metadata) is
  answered with BL-636's defaults. Every store call passes the caller (the SigV4-checked account once BL-420
  lands, anonymous before it) and scheme `s3` to BL-656's share access check; a refusal is answered as BL-417's
  ADR names it (planning `403 AccessDenied`).

## Acceptance criteria

- [ ] Named tests show:
      - a `PUT` stored and then read back;
      - a `PUT` refused when uploads are off, with the ADR's error;
      - a bad `Content-MD5` gets `BadDigest`;
      - a ListObjectsV2 of five keys with `max-keys=2` pages through with continuation tokens;
      - `delimiter=/` returns `CommonPrefixes`;
      - `Host: bucket.<listener host>` addresses the bucket.
- [ ] Each XML body in the tests is written from the API Reference's example for that operation.
- [ ] Named tests show a file put into the content store with no metadata (as FTP writes it) read and listed with
      BL-636's default content type and ETag, PutObject metadata read back by HeadObject, and a write refused by
      share access answered with the ADR's error.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-636 and BL-656; uses the shared store's metadata and access check.
