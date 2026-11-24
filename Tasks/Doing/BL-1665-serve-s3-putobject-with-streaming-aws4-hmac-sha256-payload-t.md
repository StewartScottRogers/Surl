---
id: BL-1665
title: Serve S3 PutObject with STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER bodies as the AWS SDK sends by default
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1665 — Serve S3 PutObject with STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER bodies as the AWS SDK sends by default

## Goal

The S3 server stores a `PutObject` (and `UploadPart`) whose `x-amz-content-sha256` is
`STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER` - signed `aws-chunked` chunks followed by a checksum trailer and its
`x-amz-trailer-signature` - checking every chunk signature, the trailer signature and the trailing checksum, so
the AWS SDK's default `PutObjectAsync` gets `200` instead of `501 NotImplemented`.

## Context

- Source: the Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-24T1330-Surl-bug-report.md`,
  finding 3 (Medium), against Surl v0.1.5 `d64faa4bb` on Windows 11 (seen by the recipe writers, not re-run).
  Repro, copied from the report:
  - `surl --directory store --cert server.pem --key server.key --user AKIAIOSFODNN7EXAMPLE:s3cret --allow-uploads s3://127.0.0.1:P/`
  - From C#, AWSSDK.S3 4.1.2 with `ServiceURL` pointed at it and default config:
    `PutObjectAsync(new PutObjectRequest { BucketName = "media", Key = "a.txt", ContentBody = "x" })` throws
    `AmazonS3Exception` 501 `NotImplemented`. Expected 200.
  - With `RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED` the same put gets 200. The report
    guesses an aws-chunked body with a checksum trailer, not confirmed on the wire.
- Cause, read from the code: `S3ObjectBodyReader.FormOf` maps `STREAMING-AWS4-HMAC-SHA256-PAYLOAD` to
  `SignedChunks`, `STREAMING-UNSIGNED-PAYLOAD-TRAILER` to `UnsignedChunksWithTrailer`, and every other `STREAMING-`
  value to `S3PayloadForm.Unserved`, which `ChunkedFramingRefusal` answers `501 NotImplemented`. ADR-0108 decision
  4.5 and Amendment 9 decision 5 list "the signed `-TRAILER` forms" as unserved. The AWS SDK for .NET signs
  chunks by default and, with its default flexible checksum (CRC32), sends them with a trailer, which is this
  form; `WHEN_REQUIRED` drops the trailer and falls back to the served signed form, matching the report. The
  first step of the run confirms which `x-amz-content-sha256`, `x-amz-trailer` and `Content-Encoding` the head
  carries from the AWS documentation below; no SDK package is added (base class library only).
- Oracle: this is an extended server (ADR-0083), judged by the S3 specification, never by upstream curl or the
  Curl port. Pages: "Signature Calculations for the Authorization Header: Transferring Payload in Multiple Chunks
  (Chunked Upload)" and its trailer page (https://docs.aws.amazon.com/AmazonS3/latest/API/sigv4-streaming-trailers.html):
  chunk string-to-sign `AWS4-HMAC-SHA256-PAYLOAD`, then after the final empty chunk the trailer lines and
  `x-amz-trailer-signature`, whose string-to-sign is `AWS4-HMAC-SHA256-TRAILER`, the timestamp, the scope, the
  last chunk signature and the SHA-256 of the trailer lines; and the User Guide's "Checking object integrity".
  Expected signatures in tests are computed from those pages with the BCL's `HMACSHA256`, written down in the test.
- Where: the signing key is the account's secret, held only by `Surl.Authentication` (ADR-0084 decision 4), so
  the trailer signature is checked through the chain: `IHttpChunkSignatureChain`
  (`Foundation/Surl.Protocol.Abstractions.UnitLibrary`) gains a trailer check (an interface member, no code
  there); `AwsSigV4ChunkSignatureChain` and `SignatureV4Check` (`Foundation/Surl.Authentication.UnitLibrary`,
  constant `StreamingSignedPayload`) give the trailer form a chain; `S3PayloadForm`, `S3ObjectBodyReader`,
  `S3AwsChunkedDecoder` and `S3ChecksumRequest` (`Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary`)
  decode it and check its checksum as the unsigned-trailer form does. BL-711 built the two served forms; follow it.
- Stand-in: the object lands in the shared content store and publishes the same `StoreChangedEvent` and
  exchange events a plain `PutObject` does; nothing new to seed, reset or fault (the existing S3 fault points apply).

## Acceptance criteria

- [ ] Named tests in `Foundation/Surl.Authentication.UnitTests` show a `STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER`
      request gets a chain that verifies each chunk and then the trailer signature, and refuses a tampered
      trailer line or trailer signature.
- [ ] A named regression test in `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests` replays a
      `PutObject` with that form and an `x-amz-checksum-crc32` trailer and gets `200` with the object stored and
      the checksum echoed.
- [ ] Named S3 tests show a bad trailer signature `403 SignatureDoesNotMatch` and a wrong trailing checksum
      `400 BadDigest`, neither stored, and `STREAMING-AWS4-ECDSA-P256-SHA256-PAYLOAD` still `501 NotImplemented`.
- [ ] Notes record the decision, marked "Decided by Claude under Stewart's delegation", that the signed trailer
      form is served, and the run files a `docs` task to amend ADR-0108 and `Documentation/Conformance/Extended/S3.md`.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      Authentication and S3 libraries keep 100% line and branch coverage on every member this task changes; no
      test needs `TestCategory=Integration`.

## Notes

- If confirming the head shows a different cause than the signed trailer form, record what the head is in Notes,
  fix that cause instead, and keep the same regression test shape.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
