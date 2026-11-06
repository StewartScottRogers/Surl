---
id: BL-711
title: Accept S3 presigned URLs, aws-chunked payloads and checksum headers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-420]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-11-05
completed:
---
# BL-711 — Accept S3 presigned URLs, aws-chunked payloads and checksum headers

## Goal

The S3 server serves SigV4 presigned query strings, `aws-chunked` bodies (`STREAMING-AWS4-HMAC-SHA256-PAYLOAD`
and `STREAMING-UNSIGNED-PAYLOAD-TRAILER`) and checks `x-amz-checksum-crc32`, `-crc32c`, `-sha1` and `-sha256`.

## Context

- Decision: ADR-0108 decision 4.5; matrix rows `S3:SigV4-QueryString-1` and `-2`, `S3:SigV4-Chunked-1` to `-3`,
  `S3:PutObject-6` and `-7`.
- Specification: "Authenticating Requests: Using Query Parameters (AWS Signature Version 4)", "Signature
  Calculations for the Authorization Header: Transferring Payload in Multiple Chunks (Chunked Upload)" (its worked
  example is a test vector) and the User Guide's "Checking object integrity".
- The query-string check goes beside the header check in `Surl.Authentication`'s SigV4 method, reached through
  `IHttpAuthenticationSession` (ADR-0084 decision 4); the server never references `Surl.Authentication`.
- CRC-32 and CRC-32C are hand-written where the BCL has none (`System.IO.Hashing` is a package); check whether
  `Surl.Compression` or the Kafka server already has a CRC-32C to move rather than copy (ADR-0105).

## Acceptance criteria

- [ ] A named test accepts the "Using Query Parameters" page's worked example at its date, and refuses it expired
      with `403 AccessDenied`.
- [ ] A named test accepts the chunked-upload page's worked example, and a tampered chunk is `403
      SignatureDoesNotMatch`.
- [ ] Named tests show each checksum header checked (`400 BadDigest`) and echoed, and `crc64nvme` `501
      NotImplemented`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every library touched keeps 100% line
      and branch coverage.

## Notes

Filed by BL-417 (ADR-0108): AWS's SDKs and CLI send these forms.

## Log

- 2026-11-05: Created.
