---
id: BL-419
title: Answer S3 PutObject, ListObjectsV2 and virtual-hosted requests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-418]
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

## Acceptance criteria

- [ ] Named tests show:
      - a `PUT` stored and then read back;
      - a `PUT` refused when uploads are off, with the ADR's error;
      - a bad `Content-MD5` gets `BadDigest`;
      - a ListObjectsV2 of five keys with `max-keys=2` pages through with continuation tokens;
      - `delimiter=/` returns `CommonPrefixes`;
      - `Host: bucket.<listener host>` addresses the bucket.
- [ ] Each XML body in the tests is written from the API Reference's example for that operation.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
