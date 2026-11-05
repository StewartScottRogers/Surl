---
id: BL-420
title: Require and check AWS Signature Version 4 on S3 requests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-419]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
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
- Shared accounts and stores (Stewart, 2026-11-04; BL-622's ADR decision 4): the access key is an account of the
  one account set (`--user`, `--user-file`, and later `--mailbox`), never a private key list; the checked account
  is what BL-419's store calls pass to BL-656's share access check.

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
- [ ] A named test shows the SigV4-checked account name reaching the content store's access check.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-05, BL-417: ADR-0108 decision 4.4 has this task add a refusal reason (planning
  `HttpAuthenticationRefusal`) to `HttpAuthenticationVerdict` and a hash-mismatch outcome to
  `IHttpRequestBodyCheck`, set by the SigV4 method in `Surl.Authentication`, so the S3 server can tell
  `InvalidAccessKeyId`, `SignatureDoesNotMatch` and `RequestTimeTooSkewed` apart. The HTTP server ignores both.
  `touches` gained both libraries and their tests. Decision 4.3 lists every refusal's status and code.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: access keys from the one account set, checked account passed to the store.
