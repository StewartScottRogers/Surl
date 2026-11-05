---
id: BL-420
title: Require and check AWS Signature Version 4 on S3 requests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-419]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-420 — Require and check AWS Signature Version 4 on S3 requests

## Goal

By default every S3 request must carry a valid SigV4 `Authorization` header for an account in the accounts file.
A request that fails is refused with the S3 error code BL-417's ADR names for that failure. Anonymous reads
are allowed only under `--allow-anonymous`.

## Context

- Decision: BL-417's ADR, decision 4.
- The check: reuse `Surl.Authentication`'s `AwsSigV4*` types through the contracts in `Surl.Protocol.Abstractions`
  (`IHttpAuthenticationSession`, `HttpAuthenticationRequest`), as the HTTP server does for `curl --aws-sigv4`
  (ADR-0043, ADR-0045). Do not reference `Surl.Authentication` from the server. If a contract is missing, the
  ADR says where it goes.
- Specification: "Authenticating Requests (AWS Signature Version 4)" and "Signature Calculations for the
  Authorization Header: Transferring Payload in a Single Chunk", including the worked GET-object example on that
  page (access key `AKIAIOSFODNN7EXAMPLE`, its secret, and the expected signature), which is a test vector here.
  Also `x-amz-content-sha256` (with `UNSIGNED-PAYLOAD`) and `x-amz-date` skew.

## Acceptance criteria

- [ ] A named test replays the API Reference's worked GET-object example and accepts it at the example's date (a
      fixed `TimeProvider`).
- [ ] Named tests show each refusal with its status and error code:
      - no `Authorization` with anonymous off;
      - an unknown access key gets `InvalidAccessKeyId`;
      - a bad signature gets `SignatureDoesNotMatch`;
      - a date 16 minutes off gets `RequestTimeTooSkewed`;
      - a body whose SHA-256 differs from `x-amz-content-sha256` gets `XAmzContentSHA256Mismatch`.
- [ ] A named test shows an anonymous `GET` allowed under `--allow-anonymous` and an anonymous `PUT` still refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
