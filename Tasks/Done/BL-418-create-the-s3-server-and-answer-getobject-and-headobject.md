---
id: BL-418
title: Create the S3 server and answer GetObject and HeadObject
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-417, BL-404, BL-407]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-418 — Create the S3 server and answer GetObject and HeadObject

## Goal

`Surl.Extended.Protocol.S3.UnitLibrary` exists and answers path-style `GET` and `HEAD` of an object from the
content store. It answers a missing key or bucket, and an unsupported operation, with the S3 XML error body.

## Context

- Decision: BL-417's ADR, decisions 1 to 3. Rules: BL-543's ADR (oracle, isolation, copying).
- New projects: `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary` and `.UnitTests`, listed in
  `Surl.slnx` under `/Protocols/FileTransfer/`. Namespace `Surl.Extended.Protocol.S3`. It references
  `Surl.Protocol.Abstractions`, `Surl.Content` and the HTTP/1.1 library from BL-407, nothing else
  (`ProtocolIsolationTests`, extended by BL-404).
- The server implements `IConnectionProtocolServer` (ADR-0004). Tests replay requests through `InMemoryConnection`.
- Expected bytes come from the S3 API Reference's GetObject and HeadObject pages and "Error responses"
  (`ETag`, `Last-Modified`, `Content-Length`, `x-amz-request-id`, the `<Error><Code>…</Code></Error>` body).
  Never from the port. Authentication is BL-420's, so this task's tests run with anonymous reads allowed.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - `GET /bucket/key` returns the stored bytes with the headers the ADR lists;
      - `HEAD` returns the same headers and no body;
      - a missing key gets `404 NoSuchKey`;
      - a missing bucket gets `404 NoSuchBucket`;
      - a range request is answered as the ADR says;
      - an unsupported operation gets the ADR's error;
      - an over-limit request is refused as ADR-0006 says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- 2026-11-05: Built in the session against ADR-0108 (no `protocol-architect` stage: the ADR is the plan).
  `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.S3.UnitLibrary`: 100% line, 100% branch,
  0 failing members, worst CRAP 10. 89 tests in `Surl.Extended.Protocol.S3.UnitTests`;
  `ProtocolIsolationTests` (404 tests) passes.
- The criteria's named tests, all in `S3ProtocolServerTests`:
  `ServeAsync_GetObject_ReturnsTheStoredBytesWithTheObjectHeaders`, `ServeAsync_HeadObject_ReturnsTheSameHeadersAndNoBody`,
  `ServeAsync_MissingKey_Is404NoSuchKey`, `ServeAsync_MissingBucket_Is404NoSuchBucket`,
  `ServeAsync_RangeRequest_Is206WithContentRange` (with `ServeAsync_UnsatisfiableRange_Is416InvalidRange` and
  `ServeAsync_SeveralRanges_ServesTheWholeObject`), `ServeAsync_UnsupportedOperation_Is501NotImplemented`, and
  `ServeAsync_HeadPastMaxRequestHead_IsRefused431AndClosedAsAdr0006Says` plus `ServeAsync_KeyOver1024Bytes_Is400KeyTooLongError`
  and `WriteRefusalAsync_PastAConnectionLimit_Writes503SlowDownAndCloses` for the limits.
- References: Abstractions, Content and HttpServing only; `HttpStatus` reaches it through HttpServing's own
  reference to `Surl.HttpMessage`, so no extra project reference was added.
- Choices made under the delegation (sensible defaults inside ADR-0108):
  - **Over-limit head is `431`, not ADR-0108's `400 RequestHeaderSectionTooLarge`.** `Surl.HttpServing` refuses a
    head it cannot read before any handler sees it, and it is outside this task's `touches`. The `431` with
    `Connection: close` is ADR-0006 section 5's HTTP answer, which the criterion names. Filed BL-720 for the seam.
  - **Not yet served operations are `501 NotImplemented`** (PutObject, ListObjectsV2, ListBuckets, HeadBucket,
    DeleteObject, multipart); BL-419, BL-712 and BL-713 replace those rows. **Any query on an object read is
    `501`** too, until presigned URLs (BL-711) and the `response-*` overrides are served.
  - **Reads are anonymous** (`ContentCaller.Anonymous("s3")`) until BL-420 adds SigV4; the server is not composed
    into `surl` until BL-421, so nothing serves it unauthenticated meanwhile.
  - **Echoed client values** (`Resource`, `Key`, `BucketName`) are sent as the client sent them, still
    percent-encoded: always valid XML, and the same rule as ADR-0108's `Resource`.
  - **Every answer carries `Date` and `Server: surl`**, as `Surl.HttpServing`'s own refusals do, then
    `x-amz-request-id`. A HEAD error carries `Content-Type: application/xml` and no `Content-Length` or body.
  - **Properties**: `ObjectMetadataIndex` keys files relative to the served root, so a file in a share or behind a
    `--bucket` name gets the index's defaults computed by the server (MD5 ETag, `application/octet-stream`).
  - **Conditional dates** are compared at whole-second precision (HTTP dates have no fraction); a date that does
    not parse is ignored; an `If-Match` tag sent without quotes also matches.
  - **Error message texts**: `NoSuchKey` "The specified key does not exist.", `NoSuchBucket` "The specified bucket
    does not exist.", `InvalidRange` "The requested range is not satisfiable", `PreconditionFailed` "At least one
    of the preconditions you specified did not hold.", `KeyTooLongError` "Your key is too long.", `SlowDown`
    "Please reduce your request rate.", `NotImplemented` as ADR-0108 writes it.
- Follow-up filed: BL-720 (the `400 RequestHeaderSectionTooLarge` head refusal; needs a `Surl.HttpServing` seam).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Extended.Protocol.S3 answers path-style GetObject and HeadObject from the content store (ranges, conditions, stored metadata) with S3 XML errors and 501 NotImplemented for everything else
