---
id: BL-1701
title: Carry the SigV4 canonical request and string to sign on a BadSignature verdict
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1701 — Carry the SigV4 canonical request and string to sign on a BadSignature verdict

## Goal

A refused AWS Signature Version 4 verdict whose `Refusal` is `BadSignature` carries the canonical request and the
string to sign the server computed, so the S3 server can write them into its `SignatureDoesNotMatch` error (BL-1702).

## Context

- Source: Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-25T1430-Surl-bug-report.md`, item 4
  (Low), against Surl v0.1.8 `9f8005dff`. Repro, copied from the report: the server of item 2
  (`surl --directory store --cert server.pem --key server.key --user K:S s3://127.0.0.1:18443/`);
  `curl ... --aws-sigv4 aws:amz:us-east-1:s3 -u K:wrong https://photos.s3.us-east-1.amazonaws.com/hello.txt` gives
  403 with `<Code>SignatureDoesNotMatch</Code>` but no `<CanonicalRequest>` or `<StringToSign>` elements.
  Expected: both, as AWS's error has them.
- ADR-0108 rejected sending them ("a debugging aid that tells a probing peer how the request was canonicalised").
  Decision (Claude under Stewart's delegation; BL-1702 records it as an ADR-0108 amendment): send them. Both are
  computed only from the client's own request and the public SigV4 algorithm and hold no secret of the server's;
  a stand-in's job is to let a test author see why a signature differs, which is what AWS's elements are for.
  One exception keeps ADR-0182 whole: the value of a signed `x-amz-security-token` header, or an
  `X-Amz-Security-Token` query parameter, is written as `****` in the carried canonical request, because the sent
  bytes reach the `-v` log unredacted and ADR-0182 withholds that token on receipt. The string to sign is the
  genuine one (its hash line is of the real canonical request).
- Where:
  - `Foundation/Surl.Protocol.Abstractions.UnitLibrary`: a new record `AwsSigV4SignatureMismatch(string
    CanonicalRequest, string StringToSign)` and an optional last parameter
    `AwsSigV4SignatureMismatch? SignatureMismatch = null` on `HttpAuthenticationVerdict`, with doc comments.
  - `Foundation/Surl.Authentication.UnitLibrary`: `AwsSigV4AuthenticationMethod` (line 195 refuses with
    `BadSignature`), `AwsSigV4CanonicalRequest`, `AwsSigV4Calculation`, and the session's mapping to the verdict
    (`HttpAuthenticationSession`, which builds refusals around line 230-280) fill it for the `Authorization`
    form, the presigned query form, and a header signature whose check waits for the body. `aws-chunked` chunk
    signature failures are out of scope.
- Stand-in: no store, no event change.

## Acceptance criteria

- [x] Named tests in `Foundation/Surl.Authentication.UnitTests/AwsSigV4AuthenticationMethodTests.cs`: a request
      signed with a wrong secret is refused `BadSignature` and its verdict's `SignatureMismatch` holds the canonical
      request and string to sign of AWS's documented SigV4 "GET Object" example (`examplebucket`,
      `AKIAIOSFODNN7EXAMPLE`; the vectors `AwsSigV4ServiceSignatureTests.cs` already uses), byte for byte.
- [x] Named tests in `AwsSigV4PresignedAndChunkedTests.cs`: the same for a presigned URL, and for a body-bound
      signature refused at the body check.
- [x] Named test: a signed `x-amz-security-token` appears as `x-amz-security-token:****` in the carried canonical
      request, and the string to sign's last line is the SHA-256 of the real canonical request.
- [x] Named tests: every other refusal, and every accepted verdict, has `SignatureMismatch` null.
- [x] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` and
      `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` are clean, the fast tests of the
      whole solution are green, and both libraries stay at 100% line and branch coverage.

## Notes

- Four projects because the contract addition is a record and one optional parameter; the writing of the elements
  is BL-1702.
- Pipeline: the task text was already the plan, so plan, tests (20 failing first), implementation and docs were done
  in-session; review was delegated to `code-reviewer` (no Must fix). Conformance skipped: no bytes sent change until
  BL-1702. Verify: build with `-warnaserror` clean, `dotnet format --verify-no-changes` clean, fast tests of the whole
  solution green (Authentication 1518, Abstractions 635), both libraries 100% line and branch.
- Decided by Claude under Stewart's delegation (BL-1702 records it in ADR-0108): `****` is written for every signed
  header ADR-0182 withholds (`Authorization`, `Proxy-Authorization`, `X-Api-Key`, `Api-Key`, `X-Goog-Api-Key`,
  `Ocp-Apim-Subscription-Key`, `X-Amz-Security-Token`), not the security token alone, because upstream curl signs
  headers given with `-H`, so an API key could otherwise reach the `-v` log in the sent error.
- Accepted limit: the string to sign hashes the real canonical request, so a guessable withheld value can be tested
  against it. The task requires the real hash.
- Cloud Storage's `GOOG4-HMAC-SHA256` shares `SignatureV4Check.VerifySignature`, so its bad signatures carry the
  mismatch too: it is Signature Version 4 and holds no server secret.
- `null` as well: an unknown access key, a `BadSignature` with no canonical request to build (a signed header missing,
  a target with no path) and a right signature refused by a `refuse-login` fault (nothing mismatched).
- Filed BL-1712 (one shared list of withheld header names).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A BadSignature Signature Version 4 verdict carries the canonical request (withheld values as ****) and string to sign
