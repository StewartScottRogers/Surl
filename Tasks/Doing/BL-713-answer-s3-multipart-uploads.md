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
completed:
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

- [ ] A named test uploads three parts (the first two 5 MiB), completes them, and reads back one object whose
      ETag ends `-3`.
- [ ] Named tests show `InvalidPart`, `InvalidPartOrder`, `EntityTooSmall`, `MalformedXML`, `NoSuchUpload` and
      `EntityTooLarge` each refused as ADR-0108's table says.
- [ ] Named tests show a staged part not visible through `ContentStore` before completion, AbortMultipartUpload
      removing the parts, and the 1001st open upload refused `503 SlowDown`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-417 (ADR-0108): the Curl port's client sends multipart uploads for large objects.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
