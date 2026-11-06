---
id: BL-713
title: Answer S3 multipart uploads
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-419, BL-636]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-713 — Answer S3 multipart uploads

## Goal

The S3 server answers CreateMultipartUpload, UploadPart, CompleteMultipartUpload and AbortMultipartUpload, so a
client uploading a large object in parts (as the Curl port's client and the AWS CLI do) stores one file.

## Context

- Decision: ADR-0108 decisions 1, 3 and 7; matrix rows `S3:CreateMultipartUpload-1`, `S3:UploadPart-1` and
  `-2`, `S3:CompleteMultipartUpload-1` and `-2`, `S3:AbortMultipartUpload-1`, `S3:Surl-Multipart-1` and
  `-2`.
- Specification: the S3 API Reference pages of the four operations and the User Guide's "Checking object
  integrity" (the multipart ETag `"<hex MD5 of the parts' MD5s>-<count>"`).
- Staging under `<path>/.surl/objects/uploads/<upload id>/` (ADR-0100 decision 2.3), invisible to every
  protocol until completion; the completed object is bounded by `--max-filesize`; 1000 open uploads, 24 hours
  idle, timed on `TimeProvider`.

## Acceptance criteria

- [x] A named test uploads three parts (the first two 5 MiB), completes them, and reads back one object whose
      ETag ends `-3`.
- [x] Named tests show `InvalidPart`, `InvalidPartOrder`, `EntityTooSmall`, `MalformedXML`, `NoSuchUpload` and
      `EntityTooLarge` each refused as ADR-0108's table says.
- [x] Named tests show a staged part not visible through `ContentStore` before completion, AbortMultipartUpload
      removing the parts, and the 1001st open upload refused `503 SlowDown`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-417 (ADR-0108): the Curl port's client sends multipart uploads for large objects.

- Delivered (2026-11-06): `S3CreateMultipartUpload`, `S3UploadPart`, `S3CompleteMultipartUpload`,
  `S3AbortMultipartUpload`, `S3MultipartUploads` (one per server), `S3MultipartUpload`, `S3UploadedPart`,
  `S3CompletedPart`, `S3CompleteMultipartUploadDocument`, `S3BoundedBodyStream`; five new `S3Error`s
  (`NoSuchUpload`, `InvalidPart`, `InvalidPartOrder`, `EntityTooSmall`, `MalformedXML`); four new `S3Operation`s
  recognised from an object request's query. PutObject's checks, folder creation and metadata storing are shared.
- Acceptance tests: `S3CompleteMultipartUploadTests.ServeAsync_ThreePartsTheFirstTwo5MiB_CompleteIntoOneObjectWhoseETagEndsDash3`;
  `S3CompleteMultipartUploadTests` `..._APartNotUploaded_Is400InvalidPartAndWritesNothing`,
  `..._PartsOutOfOrder_Is400InvalidPartOrderAndTheUploadStaysOpen`, `..._APartButTheLastBelow5MiB_Is400EntityTooSmall`,
  `..._ABodyThatDoesNotParse_Is400MalformedXml`, `..._AnUnknownUploadId_Is404NoSuchUpload`;
  `S3UploadPartTests.ServeAsync_APartAnnouncedPastMaxFilesize_Is400EntityTooLarge` and
  `..._PartsTogetherPastMaxFilesize_Are400EntityTooLargeAndAbortTheUpload`;
  `S3UploadPartTests.ServeAsync_AStagedPart_IsNotVisibleThroughTheContentStoreBeforeCompletion`;
  `S3AbortMultipartUploadTests.ServeAsync_AbortMultipartUpload_Answers204AndRemovesTheParts`;
  `S3CreateMultipartUploadTests.ServeAsync_The1001stOpenUpload_Is503SlowDown`; `S3MultipartUploadsTests` (24 hours
  idle). S3 tests 307, all green; `dotnet build -warnaserror` clean; every fast test green.
  `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.S3.UnitLibrary`: 100% line, 100% branch. Its three
  remaining Cobertura complexity findings (`S3ListRequest.Refusal`, `S3ListPage.Names`,
  `S3PutObject.BodyFramingRefusal`) predate this task, pass CA1502, and are BL-743's.
- Choices (Decided by Claude under Stewart's delegation; ADR-0108's record of them is BL-926):
  - Where parts are staged: ADR-0108 and ADR-0100 say `.surl/objects/uploads/<id>/`, but `ContentStore` refuses
    every write under `/.surl`, and this task touches only the S3 library (Surl.Content is held by BL-686). So, as
    BL-715 did for Azure blocks, each upload's parts sit one after another in a `ContentUploadSession` opened at
    the key and never committed: a hidden dot-file beside the object, invisible to every protocol, deleted on
    abort, expiry or completion. Completion copies the named parts into a fresh session and commits it.
  - The key's folders are created at CreateMultipartUpload, because the staging file sits in them; the key's
    mapping, `409` conflicts and `MetadataTooLarge` are checked there too.
  - The staged bytes of one upload together are held to `--max-filesize` (the session's own limit): past it the
    upload is aborted, `400 EntityTooLarge`. A part uploaded again replaces the old one; the old bytes stay
    staged until the upload ends.
  - Multipart requests run one at a time per server (an upload session is not safe for concurrent use); each
    first aborts every upload untouched for more than 24 hours, so expiry needs no timer.
  - An `uploadId` created for another bucket or key is `404 NoSuchUpload`; the error carries `UploadId`.
  - Completion order: body (1 MiB, signature, `Content-MD5`), `NoSuchUpload`, `MalformedXML`, `InvalidPartOrder`
    (not strictly ascending, so a repeated number too), `InvalidPart` (ETag quotes optional, hex of either case),
    `EntityTooSmall`. A refused completion leaves the upload open, so the client may try again.
  - Unknown elements inside a `Part` (the `Checksum*` ones) are skipped; anything else unexpected is `MalformedXML`.
  - `Location` is `http` or `https` by whether the connection is TLS, then `Host` (else the listen host), then
    the path as sent.
  - ListParts, UploadPartCopy and every other object query stay `501 NotImplemented`.
- Filed: BL-926 (ADR-0108 amendment).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. The S3 server answers CreateMultipartUpload, UploadPart, CompleteMultipartUpload and AbortMultipartUpload, assembling the parts into one object with the multipart ETag
