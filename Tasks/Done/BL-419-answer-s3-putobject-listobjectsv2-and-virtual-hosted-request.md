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
completed: 2026-11-05
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

- [x] Named tests show:
      - a `PUT` stored and then read back;
      - a `PUT` refused when uploads are off, with the ADR's error;
      - a bad `Content-MD5` gets `BadDigest`;
      - a ListObjectsV2 of five keys with `max-keys=2` pages through with continuation tokens;
      - `delimiter=/` returns `CommonPrefixes`;
      - `Host: bucket.<listener host>` addresses the bucket.
- [x] Each XML body in the tests is written from the API Reference's example for that operation.
- [x] Named tests show a file put into the content store with no metadata (as FTP writes it) read and listed with
      BL-636's default content type and ETag, PutObject metadata read back by HeadObject, and a write refused by
      share access answered with the ADR's error.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

What was built (2026-11-05): `S3OperationRecognition` routes GetObject/HeadObject, PutObject and
ListObjectsV2; `S3PutObject` streams the body through `S3UploadStream` into a content-store upload
session, hashing MD5 as it goes; `S3ListObjectsV2` walks the bucket (`S3BucketWalk`, UTF-8 binary
order), pages (`S3ListPage`, `S3ContinuationToken`) and writes `ListBucketResult`; `S3VirtualHost`
reads the bucket from `Host`. Tests: `S3PutObjectTests`, `S3ListObjectsV2Tests`,
`S3VirtualHostTests` (172 in the project, 100% line and branch coverage of the library).

Choices made, each to be written into ADR-0108 by BL-721 (the ADR folder was held by BL-463, so it
could not be edited here without overlapping):

- `--allow-uploads` and `--max-filesize` are read from the `ContentStore`'s `ExposureOptions`, which
  the server's constructor receives - not a static, and no second constructor parameter that could
  disagree with the store.
- The listen host for virtual hosting is read from `ExchangeContext.ListenUrl.Host` per exchange,
  rather than a constructor parameter: the same value, with no change to the composition (BL-421).
- ListObjectsV2 is a directory listing in ADR-0006's sense, so it needs `--list-directories`; off,
  it is `403 AccessDenied` (secure by default, loosened for tests).
- `Surl.HttpServing` drops a refusal's body, so `EntityTooLarge` and `IncompleteBody` are ordinary
  error answers: an announced body past the limit is left unread, which closes the connection after
  the lingering drain. Closing without the drain is BL-723.
- A body that ends early is `400 IncompleteBody` (S3's "List of error codes"), a code ADR-0108's
  table did not list.
- A plain chunked PUT is `400 InvalidRequest`; one with `x-amz-decoded-content-length` is
  `aws-chunked`, `501` until BL-711.
- The metadata index key is the path under the served root with a `--bucket` name replaced by its
  content path (`S3IndexKey`); a file in a share has no index key, so its metadata is not kept and it
  answers the computed defaults. This also fixes BL-418's lookup of a `--bucket` object, which used
  the bucket name as the key.
- A PutObject whose metadata the index refuses (past its 8 KiB bound, or index full) removes the
  file just written and answers `400 MetadataTooLarge` or `503 ServiceUnavailable`.
- A PutObject creates the key's missing folders; a file in the way, or a folder at the key, is
  `409 InvalidArgument`. A zero-byte PUT of `x/` creates the folder, but ListObjectsV2 does not list
  it yet: BL-722.

Follow-ups filed: BL-721 (ADR amendment), BL-722 (folder objects in listings), BL-723 (refusal
bodies with close).

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-636 and BL-656; uses the shared store's metadata and access check.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. S3 PutObject, ListObjectsV2 with paging, prefixes and delimiters, and virtual-hosted addressing are served
